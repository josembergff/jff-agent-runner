using JffCsharpTools.Application.Common;

namespace domain.interfaces.services;

public interface IExecutionService
{
    Task<Result> StartMenuAsync();
    Task<Result> ShowRunningAsync();
    Task<Result> ShowHistoryAsync();
}