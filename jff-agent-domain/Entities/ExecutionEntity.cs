using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;
using JffCsharpTools.Domain.Entity;
using JffCsharpTools.Domain.Extensions;

namespace Domain.Entities;

public class Execution : DefaultEntity
{
    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    [ForeignKey(nameof(Agent))]
    public int AgentId { get; set; }
    public int IssueNumber { get; set; }
    public ExecutionStatusEnum Status { get; set; }
    public virtual Project Project { get; set; }
    public virtual Agent Agent { get; set; }

    [NotMapped]
    public string StatusName { get { return Status.GetDescription(); } }
}