using Domain.DTOs.Integrations;
using JffCsharpTools.Application.Common;

namespace Domain.Interfaces.Infrastructure;

public interface IGitHubCli
{
    Task<Result<GitHubIssueDto>> GetIssueAsync(
        string repository,
        int issueNumber);
}