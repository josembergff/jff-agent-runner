using domain.interfaces.services;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Infrastructure;
using Domain.Prompts;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Services;
using JffCsharpTools.Domain.Exceptions;
using JffCsharpTools.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Domain.Services;

public class ExecutionService : DefaultService, IExecutionService
{
    private readonly ITmuxExecutor tmuxExecutor;
    private readonly IExecutionWorkspace executionWorkspace;
    private readonly ILogger<ExecutionService> logger;

    public ExecutionService(IDefaultRepository repository, ITmuxExecutor tmuxExecutor, IExecutionWorkspace executionWorkspace, ILogger<ExecutionService> logger) : base(repository)
    {
        this.tmuxExecutor = tmuxExecutor;
        this.executionWorkspace = executionWorkspace;
        this.logger = logger;
    }
    public async Task<Result> CreateAsync(Execution execution)
    {
        // 1. Load project
        var project = await defaultRepository.GetByKey<Project, int>(
            execution.ProjectId);

        if (project is null)
            throw new DomainException("Project not found.");

        // 2. Load agent
        var agent = await defaultRepository.GetByKey<Domain.Entities.Agent, int>(
            execution.AgentId);

        if (agent is null)
            throw new DomainException("Agent not found.");

        // 4. Generate session name
        var sessionName = $"jff-{execution.Id}";
        execution.SessionName = sessionName;
        execution.SesionAgentCommand = agent.Command;
        execution.SessionProjectPath = string.Empty;
        execution.Status = ExecutionStatusEnum.Pending;
        execution.CreatedAt = DateTime.UtcNow;
        execution.SessionDetails = $"Session name temporary '{sessionName}'";

        // 5. Save execution before starting the agent
        var resultSave = await defaultRepository.Create(execution, true);
        sessionName = $"jff-{resultSave.Id}";
        execution.SessionName = sessionName;
        await defaultRepository.UpdateByKey(execution, resultSave.Id, true);
        execution = await defaultRepository.GetByKey<Execution, int>(resultSave.Id);

        try
        {
            // Each execution gets its own writable clone so agents never share a working tree.
            var workspaceResult = await executionWorkspace.CreateAsync(project, resultSave.Id);
            if (!workspaceResult.Success || string.IsNullOrWhiteSpace(workspaceResult.Value))
                throw new DomainException($"Could not create the execution checkout: {workspaceResult.Error}");

            var executionPath = workspaceResult.Value;
            execution.SessionProjectPath = executionPath;
            await defaultRepository.UpdateByKey(execution, execution.Id, true);

            var instructions = IssueImplementationPrompt.Build(execution.IssueNumber);
            var agentName = Path.GetFileName(agent.Command);
            var isCodex = string.Equals(agentName, "codex", StringComparison.OrdinalIgnoreCase);
            var isClaude = string.Equals(agentName, "claude", StringComparison.OrdinalIgnoreCase);
            var isOpenCode = string.Equals(agentName, "opencode", StringComparison.OrdinalIgnoreCase);
            var promptAtStartup = isCodex || isClaude || isOpenCode;
            var agentArguments = agent.Arguments;

            if (isCodex)
            {
                var isCodexExec = agentArguments
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(argument => string.Equals(argument, "exec", StringComparison.Ordinal));
                if (isCodexExec)
                {
                    // The exec subcommand accepts approval policy through config, not
                    // --ask-for-approval; remove it if it was copied from TUI examples.
                    agentArguments = Regex.Replace(
                        agentArguments,
                        @"(?<!\S)--ask-for-approval(?:\s+\S+|=\S+)",
                        string.Empty,
                        RegexOptions.CultureInvariant).Trim();
                    agentArguments = AppendConfigOverrideIfMissing(agentArguments, "approval_policy=\"never\"");
                }
                else
                    agentArguments = AppendOptionIfMissing(agentArguments, "--ask-for-approval", "never");

                agentArguments = AppendOptionIfMissing(agentArguments, "--sandbox", "workspace-write");
                agentArguments = AppendConfigOverrideIfMissing(
                    agentArguments,
                    "sandbox_workspace_write.network_access=true");
            }
            else if (isClaude)
            {
                agentArguments = AppendFlagIfMissing(agentArguments, "--dangerously-skip-permissions");
            }
            else if (isOpenCode)
            {
                agentArguments = AppendFlagIfMissing(agentArguments, "--auto");
            }

            // Codex asks whether each new checkout is trusted before it accepts a prompt.
            // Scope trust to this isolated checkout and process; do not modify global
            // configuration or authentication files.
            if (isCodex)
                agentArguments = AddCodexTrustOverride(agentArguments, executionPath);

            // 6. Start agent session
            var resultTmux = await tmuxExecutor.StartSessionAsync(
                sessionName,
                executionPath,
                agent.Command,
                agentArguments,
                promptAtStartup ? instructions : null,
                isOpenCode ? "--prompt" : null);

            if (!resultTmux.Success)
            {
                logger.LogError("Failed to start session {SessionName}: {Error}", sessionName, resultTmux.Error);
                throw new DomainException(resultTmux.Error);
            }

            // The supported CLIs accept the initial prompt natively. This also lets them
            // process first-run workspace dialogs before the task starts. Other CLIs use
            // the shared bracketed-paste delivery after their terminal is ready.
            if (!promptAtStartup)
            {
                var resultInstructions = await tmuxExecutor.SendInstructionsAsync(sessionName, instructions);

                if (!resultInstructions.Success)
                {
                    logger.LogError("Failed to send instructions to session {SessionName}: {Error}", sessionName, resultInstructions.Error);
                    await tmuxExecutor.KillSessionAsync(sessionName);
                    throw new DomainException(resultInstructions.Error);
                }
            }

            // 8. Update execution status
            execution.Status = ExecutionStatusEnum.Running;
            execution.StartedAt = DateTime.UtcNow;
            execution.UpdatedAt = DateTime.UtcNow;
            execution.SessionDetails = $"Execution started by {agent.Name} in session with the instructions for issue #{execution.IssueNumber}";

            await defaultRepository.UpdateByKey(execution, execution.Id, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(execution.SessionProjectPath))
            {
                var cleanupResult = await executionWorkspace.DeleteAsync(
                    project.LocalPath,
                    execution.SessionProjectPath);
                if (!cleanupResult.Success)
                    logger.LogError(
                        "Failed to clean execution checkout {WorkspacePath}: {Error}",
                        execution.SessionProjectPath,
                        cleanupResult.Error);
            }

            execution.Status = ExecutionStatusEnum.Failed;
            execution.FinishedAt = DateTime.UtcNow;
            execution.UpdatedAt = DateTime.UtcNow;
            execution.SessionDetails = ex.Message;

            await defaultRepository.UpdateByKey(execution, execution.Id, true);

            return Result.Fail(ex);
        }
    }

    private static string AddCodexTrustOverride(string arguments, string executionPath)
    {
        var tomlPath = executionPath
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
        var configValue = $"projects.\"{tomlPath}\".trust_level=\"trusted\"";
        var trustOverride = $"--config {QuoteForShell(configValue)}";
        return string.IsNullOrWhiteSpace(arguments)
            ? trustOverride
            : $"{arguments} {trustOverride}";
    }

    private static string AppendOptionIfMissing(string arguments, string option, string value) =>
        arguments.Contains($"{option} {value}", StringComparison.Ordinal)
            ? arguments
            : $"{arguments} {option} {value}".Trim();

    private static string AppendFlagIfMissing(string arguments, string flag) =>
        arguments.Contains(flag, StringComparison.Ordinal)
            ? arguments
            : $"{arguments} {flag}".Trim();

    private static string AppendConfigOverrideIfMissing(string arguments, string configOverride) =>
        arguments.Contains(configOverride, StringComparison.Ordinal)
            ? arguments
            : $"{arguments} --config {QuoteForShell(configOverride)}".Trim();

    private static string QuoteForShell(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

}
