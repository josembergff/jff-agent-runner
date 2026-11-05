namespace Domain.DTOs.Integrations;

public class GitHubIssueDto
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string State { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}