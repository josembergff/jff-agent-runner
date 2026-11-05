using domain.interfaces.services;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Services;
using JffCsharpTools.Domain.Exceptions;
using JffCsharpTools.Domain.Interfaces.Repositories;

namespace Domain.Services;

public class ExecutionService : DefaultService, IExecutionService
{
    private readonly ITmuxExecutor tmuxExecutor;

    public ExecutionService(IDefaultRepository repository, ITmuxExecutor tmuxExecutor) : base(repository)
    {
        this.tmuxExecutor = tmuxExecutor;
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
        var sessionName = $"jff-{execution.Id:N}";

        execution.SessionName = sessionName;
        execution.SesionAgentCommand = agent.Command;
        execution.SessionProjectPath = project.LocalPath;
        execution.Status = ExecutionStatusEnum.Pending;
        execution.CreatedAt = DateTime.UtcNow;

        // 5. Save execution before starting the agent
        await defaultRepository.Create(execution, true);

        try
        {
            // 6. Start agent session
            await tmuxExecutor.StartSessionAsync(
                sessionName,
                project.LocalPath,
                agent.Command);

            // 7. Update execution status
            execution.Status = ExecutionStatusEnum.Running;
            execution.StartedAt = DateTime.UtcNow;

            await defaultRepository.UpdateByKey(execution, execution.Id, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            execution.Status = ExecutionStatusEnum.Failed;
            execution.FinishedAt = DateTime.UtcNow;

            await defaultRepository.UpdateByKey(execution, execution.Id, true);

            return Result.Fail(ex);
        }
    }


}