using System.Diagnostics;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;

namespace Infra.Tmux;

public class TmuxExecutor : ITmuxExecutor
{
    public async Task<Result> StartSessionAsync(
        string sessionName,
        string workingDirectory,
        string command)
    {
        try
        {
            await ExecuteAsync(
                "new-session",
                "-d",
                "-s", sessionName,
                "-c", workingDirectory,
                command);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    public async Task<Result<bool>> HasSessionAsync(string sessionName)
    {
        try
        {
            await ExecuteAsync("has-session", "-t", sessionName);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex);
        }
    }

    public async Task<Result<string>> GetOutputAsync(string sessionName)
    {
        try
        {
            var value = await ExecuteAsync(
            "capture-pane",
            "-p",
            "-S", "-100",
            "-t", sessionName);
            return Result<string>.Ok(value);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex);
        }
    }

    public async Task<Result> KillSessionAsync(string sessionName)
    {
        try
        {
            await ExecuteAsync("kill-session", "-t", sessionName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    private static async Task<string> ExecuteAsync(
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "tmux",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Fails to start tmux.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Error to run tmux: {error}");

        return output;
    }
}