using JffCsharpTools.Application.Common;

namespace Domain.Interfaces.Infrastructure;

public interface ITmuxExecutor
{
    Task<Result> StartSessionAsync(
        string sessionName,
        string workingDirectory,
        string command,
        string arguments);

    Task<Result> SendInstructionsAsync(string sessionName, string instructions);

    Task<Result<bool>> HasSessionAsync(string sessionName);

    Task<Result<string>> GetOutputAsync(string sessionName);

    Task<Result> KillSessionAsync(string sessionName);
}