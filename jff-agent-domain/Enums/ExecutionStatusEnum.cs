using System.ComponentModel;

namespace Domain.Enums;

public enum ExecutionStatusEnum
{
    [Description("Pending")]
    Pending,
    [Description("Running")]
    Running,
    [Description("Completed")]
    Completed,
    [Description("Failed")]
    Failed,
    [Description("Cancelled")]
    Cancelled
}