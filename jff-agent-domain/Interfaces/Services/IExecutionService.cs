namespace domain.interfaces.services;

public interface IExecutionService
{
    Task StartMenuAsync();
    Task ShowRunningAsync();
    Task ShowHistoryAsync();
}