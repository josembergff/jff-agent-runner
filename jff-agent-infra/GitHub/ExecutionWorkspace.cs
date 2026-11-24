using Domain.Entities;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Infra.GitHub;

public class ExecutionWorkspace(IGitHubCli gitHubCli) : IExecutionWorkspace
{
    private static readonly SemaphoreSlim CodexConfigGate = new(1, 1);

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

    public async Task<Result> TrustCodexCheckoutAsync(string projectPath, string workspacePath)
    {
        try
        {
            var fullProjectPath = Path.GetFullPath(projectPath);
            var fullWorkspacePath = Path.GetFullPath(workspacePath);
            var expectedParent = Path.Combine(fullProjectPath, "executions");
            var parent = Path.GetDirectoryName(fullWorkspacePath);
            if (!string.Equals(parent, expectedParent, StringComparison.Ordinal)
                || !IsExecutionDirectory(fullWorkspacePath))
                return Result.Fail(new InvalidOperationException(
                    "Refusing to trust a Codex path outside the project's execution workspaces directory."));

            var configPath = GetCodexConfigPath();
            var sectionHeader = GetCodexProjectSectionHeader(fullWorkspacePath);
            var marker = GetTrustMarker(fullWorkspacePath);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            await CodexConfigGate.WaitAsync();
            try
            {
                await using var fileLock = await AcquireCodexConfigLockAsync(configPath + ".jff-agent-runner.lock");
                var lines = await ReadCodexConfigLinesAsync(configPath);
                var sectionIndex = lines.FindIndex(line => string.Equals(line.TrimEnd(), sectionHeader, StringComparison.Ordinal));

                if (sectionIndex >= 0)
                {
                    var sectionEnd = FindNextTomlSection(lines, sectionIndex + 1);
                    var trustSetting = lines
                        .Skip(sectionIndex + 1)
                        .Take(sectionEnd - sectionIndex - 1)
                        .FirstOrDefault(line => IsTrustSetting(line));
                    if (trustSetting is not null)
                    {
                        if (trustSetting.Contains("\"trusted\"", StringComparison.Ordinal))
                            return Result.Ok();

                        return Result.Fail(new InvalidOperationException(
                            "The Codex user configuration already has a trust decision for this execution checkout."));
                    }

                    lines.InsertRange(sectionEnd, [marker, "trust_level = \"trusted\""]);
                }
                else
                {
                    if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                        lines.Add(string.Empty);
                    lines.Add(sectionHeader);
                    lines.Add(marker);
                    lines.Add("trust_level = \"trusted\"");
                }

                await WriteCodexConfigLinesAsync(configPath, lines);
                return Result.Ok();
            }
            finally
            {
                CodexConfigGate.Release();
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    public async Task<Result> DeleteAsync(string projectPath, string workspacePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return Result.Ok();

            var projectFullPath = Path.GetFullPath(projectPath);
            var projectExecutionsPath = Path.Combine(projectFullPath, "executions");
            var fullPath = Path.GetFullPath(workspacePath);
            if (string.Equals(fullPath, projectFullPath, StringComparison.Ordinal))
                return Result.Ok();

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
                return Result.Fail(new InvalidOperationException(
                    "Refusing to delete a path outside the project's execution workspaces directory."));

            var trustCleanup = await RemoveCodexCheckoutTrustAsync(fullPath);
            if (!trustCleanup.Success)
                return trustCleanup;

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    private static bool IsExecutionDirectory(string path)
    {
        const string prefix = "execution-";
        var name = Path.GetFileName(path);
        return name.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(prefix.Length), out _);
    }

    private static async Task<Result> RemoveCodexCheckoutTrustAsync(string workspacePath)
    {
        try
        {
            var configPath = GetCodexConfigPath();
            if (!File.Exists(configPath))
                return Result.Ok();

            var sectionHeader = GetCodexProjectSectionHeader(workspacePath);
            var marker = GetTrustMarker(workspacePath);
            await CodexConfigGate.WaitAsync();
            try
            {
                await using var fileLock = await AcquireCodexConfigLockAsync(configPath + ".jff-agent-runner.lock");
                var lines = await ReadCodexConfigLinesAsync(configPath);
                var sectionIndex = lines.FindIndex(line => string.Equals(line.TrimEnd(), sectionHeader, StringComparison.Ordinal));
                if (sectionIndex < 0)
                    return Result.Ok();

                var sectionEnd = FindNextTomlSection(lines, sectionIndex + 1);
                var markerIndex = lines.FindIndex(sectionIndex + 1, sectionEnd - sectionIndex - 1,
                    line => string.Equals(line.Trim(), marker, StringComparison.Ordinal));
                if (markerIndex < 0)
                    return Result.Ok();

                lines.RemoveAt(markerIndex);
                if (markerIndex < lines.Count && IsTrustSetting(lines[markerIndex]))
                    lines.RemoveAt(markerIndex);

                sectionEnd = FindNextTomlSection(lines, sectionIndex + 1);
                var sectionHasValues = lines
                    .Skip(sectionIndex + 1)
                    .Take(sectionEnd - sectionIndex - 1)
                    .Any(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'));
                if (!sectionHasValues)
                    lines.RemoveAt(sectionIndex);

                await WriteCodexConfigLinesAsync(configPath, lines);
                return Result.Ok();
            }
            finally
            {
                CodexConfigGate.Release();
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    private static string GetCodexConfigPath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
            codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

        return Path.Combine(Path.GetFullPath(codexHome), "config.toml");
    }

    private static string GetCodexProjectSectionHeader(string path)
    {
        var escapedPath = path
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
        return $"[projects.\"{escapedPath}\"]";
    }

    private static string GetTrustMarker(string path)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
        return $"# jff-agent-runner temporary checkout trust {hash}";
    }

    private static bool IsTrustSetting(string line) =>
        Regex.IsMatch(line, @"^\s*trust_level\s*=");

    private static int FindNextTomlSection(IReadOnlyList<string> lines, int start)
    {
        for (var index = start; index < lines.Count; index++)
        {
            if (lines[index].TrimStart().StartsWith('['))
                return index;
        }

        return lines.Count;
    }

    private static async Task<List<string>> ReadCodexConfigLinesAsync(string configPath)
    {
        if (!File.Exists(configPath))
            return [];

        var content = await File.ReadAllTextAsync(configPath);
        return content.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
    }

    private static async Task WriteCodexConfigLinesAsync(string configPath, IReadOnlyList<string> lines)
    {
        var tempPath = $"{configPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, string.Join(Environment.NewLine, lines));
            if (!OperatingSystem.IsWindows())
            {
                var mode = File.Exists(configPath)
                    ? File.GetUnixFileMode(configPath)
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite;
                File.SetUnixFileMode(tempPath, mode);
            }

            File.Move(tempPath, configPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static async Task<FileStream> AcquireCodexConfigLockAsync(string lockPath)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
        }
    }
}
