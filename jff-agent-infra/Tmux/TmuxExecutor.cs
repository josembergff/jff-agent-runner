using System.Diagnostics;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;

namespace Infra.Tmux;

public class TmuxExecutor : ITmuxExecutor
{
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StartupPollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<Result> StartSessionAsync(
        string sessionName,
        string workingDirectory,
        string command,
        string arguments)
    {
        var sessionCreated = false;
        var fullCommand = string.IsNullOrWhiteSpace(arguments)
            ? command
            : $"{command} {arguments}";

        try
        {
            // new-session returns 0 even when its command fails right away, and the
            // session disappears along with its output. So the pane is created first and
            // kept after exit, which preserves the console and the exit status.
            await ExecuteAsync("new-session", "-d", "-s", sessionName, "-c", workingDirectory);
            sessionCreated = true;

            await ExecuteAsync("set-option", "-w", "-t", sessionName, "remain-on-exit", "on");
            await ExecuteAsync("respawn-pane", "-k", "-t", sessionName, "-c", workingDirectory, fullCommand);

            var deadline = DateTime.UtcNow + StartupGracePeriod;

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(StartupPollInterval);

                var pane = (await ExecuteAsync(
                    "list-panes", "-t", sessionName,
                    "-F", "#{pane_dead}:#{pane_dead_status}")).Trim();

                if (pane.StartsWith('1'))
                    return await FinishExitedSessionAsync(sessionName, pane[2..]);
            }

            // Still alive: restore the default so the session closes when the agent exits.
            await ExecuteAsync("set-option", "-w", "-t", sessionName, "remain-on-exit", "off");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            // A failed new-session may mean the name is taken by a session that is not ours.
            if (sessionCreated)
                await KillSessionAsync(sessionName);

            return Result.Fail(ex);
        }
    }

    private async Task<Result> FinishExitedSessionAsync(string sessionName, string exitStatus)
    {
        var output = await GetOutputAsync(sessionName);
        await KillSessionAsync(sessionName);

        if (exitStatus == "0")
            return Result.Ok();

        var status = exitStatus.Length > 0 ? exitStatus : "unknown";
        var console = output.Success
            ? CleanDeadPaneOutput(output.Value)
            : $"Could not read the console: {output.Error}";

        return Result.Fail(new InvalidOperationException(
            $"Agent command exited with status {status}.\n{console}".TrimEnd()));
    }

    // Removes the padding of the empty screen and the status line tmux adds to a dead pane.
    private static string CleanDeadPaneOutput(string output) =>
        string.Join('\n', output
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0 && !line.StartsWith("Pane is dead")));

    public async Task<Result<bool>> HasSessionAsync(string sessionName)
    {
        try
        {
            await ExecuteAsync("has-session", "-t", sessionName);
            return Result<bool>.Ok(true);
        }
        catch (InvalidOperationException)
        {
            // tmux ran and exited non-zero: the session (or the tmux server) does not exist.
            return Result<bool>.Ok(false);
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