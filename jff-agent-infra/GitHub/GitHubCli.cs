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

    public async Task<Result<List<GitHubIssueDto>>> ListOpenIssuesAsync(
        string repositoryUrl)
    {
        try
        {
            // Sem --repo, o gh usaria o repositório do diretório atual.
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);

            // O gh retorna apenas 30 issues por padrão; o limite máximo garante a lista completa.
            var output = await ExecuteAsync(
                "issue",
                "list",
                "--repo",
                repositoryUrl,
                "--state",
                "open",
                "--limit",
                int.MaxValue.ToString(),
                "--json",
                "number,title,body,state,url,author,projectItems",
                "--jq",
                """[.[] | select(any(.projectItems[]?; .status.name == "Ready"))]""");

            var issues = JsonSerializer.Deserialize<List<GitHubIssueDto>>(
                output,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];

            return Result<List<GitHubIssueDto>>.Ok(issues);
        }
        catch (Exception ex)
        {
            return Result<List<GitHubIssueDto>>.Fail(ex);
        }
    }

    public async Task<Result> CloneRepositoryAsync(
        string repositoryUrl,
        string directoryPath)
    {
        try
        {
            // gh runs git clone using its own stored credentials, so private repositories work.
            await ExecuteAsync(
                "repo",
                "clone",
                repositoryUrl,
                directoryPath);

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
            FileName = "gh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Fails to start GitHub CLI.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Error to run GitHub CLI: {error}");

        return output;
    }
}