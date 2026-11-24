using Domain.Entities;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;

namespace Infra.GitHub;

public class ExecutionWorkspace(IGitHubCli gitHubCli) : IExecutionWorkspace
{
    public async Task<Result<string>> CreateAsync(Project project, int executionId)
    {
        var projectPath = Path.GetFullPath(project.LocalPath);
        var executionsPath = Path.Combine(projectPath, "executions");
        var workspacePath = Path.Combine(executionsPath, $"execution-{executionId}");

        try
        {
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(executionsPath);
            if (Directory.Exists(workspacePath))
                throw new IOException($"Execution checkout path already exists: {workspacePath}");

            var cloneResult = await gitHubCli.CloneRepositoryAsync(project.RepositoryUrl, workspacePath);
            if (!cloneResult.Success)
            {
                if (Directory.Exists(workspacePath))
                    Directory.Delete(workspacePath, recursive: true);

                return Result<string>.Fail(new InvalidOperationException(
                    cloneResult.Error ?? "Repository clone failed."));
            }

            return Result<string>.Ok(workspacePath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex);
        }
    }

    public Task<Result> DeleteAsync(string projectPath, string workspacePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return Task.FromResult(Result.Ok());

            var projectFullPath = Path.GetFullPath(projectPath);
            var projectExecutionsPath = Path.Combine(projectFullPath, "executions");
            var fullPath = Path.GetFullPath(workspacePath);
            if (string.Equals(fullPath, projectFullPath, StringComparison.Ordinal))
                return Task.FromResult(Result.Ok());

            var legacyExecutionsPath = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "jff-agent-runner",
                "executions"));

            var parentPath = Path.GetDirectoryName(fullPath);
            var isProjectExecution = string.Equals(
                parentPath,
                projectExecutionsPath,
                StringComparison.Ordinal);
            var isLegacyExecution = string.Equals(
                parentPath,
                legacyExecutionsPath,
                StringComparison.Ordinal);

            if ((!isProjectExecution && !isLegacyExecution)
                || !IsExecutionDirectory(fullPath))
                return Task.FromResult(Result.Fail(new InvalidOperationException(
                    "Refusing to delete a path outside the project's execution workspaces directory.")));

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Fail(ex));
        }
    }

    private static bool IsExecutionDirectory(string path)
    {
        const string prefix = "execution-";
        var name = Path.GetFileName(path);
        return name.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(prefix.Length), out _);
    }
}
