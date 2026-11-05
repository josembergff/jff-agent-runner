using JffCsharpTools.Domain.Entity;

namespace Domain.Entities;

public class Project : DefaultEntity
{
    public string Name { get; set; }
    public string RepositoryUrl { get; set; }
    public string LocalPath { get; set; }
}