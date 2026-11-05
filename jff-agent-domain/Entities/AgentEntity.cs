using JffCsharpTools.Domain.Entity;

namespace Domain.Entities;

public class Agent : DefaultEntity
{
    public string Name { get; set; }
    public string Command { get; set; }
    public string Arguments { get; set; }
}