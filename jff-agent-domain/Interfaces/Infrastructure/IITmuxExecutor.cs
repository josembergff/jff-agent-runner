using JffCsharpTools.Application.Common;

namespace Domain.Interfaces.Infrastructure;

public interface ITmuxExecutor
{
    Task<Result> StartSessionAsync(
        string sessionName,
        string workingDirectory,
        string command);

    Task<Result<bool>> HasSessionAsync(string sessionName);

    Task<Result<string>> GetOutputAsync(string sessionName);

    Task<Result> KillSessionAsync(string sessionName);
}