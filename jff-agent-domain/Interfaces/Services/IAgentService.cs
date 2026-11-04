using JffCsharpTools.Application.Common;

namespace domain.interfaces.services;

public interface IAgentService
{
    Task<Result> ShowMenuAsync();
}