using Domain.Entities;
using JffCsharpTools.Application.Common;

namespace Domain.Interfaces.Infrastructure;

public interface IExecutionWorkspace
{
    Task<Result<string>> CreateAsync(Project project, int executionId);

    Task<Result> DeleteAsync(string projectPath, string workspacePath);
}
