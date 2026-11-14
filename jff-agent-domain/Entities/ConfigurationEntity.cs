using JffCsharpTools.Domain.Entity;

namespace Domain.Entities;

public class Configuration : DefaultEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
