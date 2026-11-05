using System.ComponentModel;

namespace Domain.Enums;

public enum ExecutionStatusEnum
{
    [Description("Pendente")]
    Pending,
    [Description("Em execução")]
    Running,
    [Description("Concluído")]
    Completed,
    [Description("Cancelado")]
    Failed
}