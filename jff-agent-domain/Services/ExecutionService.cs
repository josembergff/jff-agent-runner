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

namespace Domain.Services;

public class ExecutionService : DefaultService, IExecutionService
{
    private readonly ITmuxExecutor tmuxExecutor;
    private readonly ILogger<ExecutionService> logger;

    public ExecutionService(IDefaultRepository repository, ITmuxExecutor tmuxExecutor, ILogger<ExecutionService> logger) : base(repository)
    {
        this.tmuxExecutor = tmuxExecutor;
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

        // 3. Validate project directory
        if (!Directory.Exists(project.LocalPath))
            throw new DomainException("Project directory does not exist.");

        // 4. Generate session name
        var sessionName = $"jff-{execution.Id}";
        execution.SessionName = sessionName;
        execution.SesionAgentCommand = agent.Command;
        execution.SessionProjectPath = project.LocalPath;
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
            // 6. Start agent session
            var resultTmux = await tmuxExecutor.StartSessionAsync(
                sessionName,
                project.LocalPath,
                agent.Command,
                agent.Arguments);

            if (!resultTmux.Success)
            {
                logger.LogError("Failed to start session {SessionName}: {Error}", sessionName, resultTmux.Error);
                throw new DomainException(resultTmux.Error);
            }

            // 7. Send the issue instructions once the agent is open
            var resultInstructions = await tmuxExecutor.SendInstructionsAsync(
                sessionName,
                IssueImplementationPrompt.Build(execution.IssueNumber));

            if (!resultInstructions.Success)
            {
                logger.LogError("Failed to send instructions to session {SessionName}: {Error}", sessionName, resultInstructions.Error);
                await tmuxExecutor.KillSessionAsync(sessionName);
                throw new DomainException(resultInstructions.Error);
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
            execution.Status = ExecutionStatusEnum.Failed;
            execution.FinishedAt = DateTime.UtcNow;
            execution.UpdatedAt = DateTime.UtcNow;
            execution.SessionDetails = ex.Message;

            await defaultRepository.UpdateByKey(execution, execution.Id, true);

            return Result.Fail(ex);
        }
    }


}