using System.Diagnostics;
using System.Text.Json;
using Domain.DTOs.Integrations;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;

public class GitHubCli : IGitHubCli
{
    public async Task<Result<GitHubIssueDto>> GetIssueAsync(
        string repository,
        int issueNumber)
    {
        try
        {
            var output = await ExecuteAsync(
                "issue",
                "view",
                issueNumber.ToString(),
                "--repo",
                repository,
                "--json",
                "number,title,body,state,url,author");

            var issue = JsonSerializer.Deserialize<GitHubIssueDto>(
                output,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new GitHubIssueDto();

            return Result<GitHubIssueDto>.Ok(issue);
        }
        catch (Exception ex)
        {
            return Result<GitHubIssueDto>.Fail(ex);
        }
    }

    private static async Task<string> ExecuteAsync(
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "gh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Falha ao iniciar gh.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Erro ao executar GitHub CLI: {error}");

        return output;
    }
}