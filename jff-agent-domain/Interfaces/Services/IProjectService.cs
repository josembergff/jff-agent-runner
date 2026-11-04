using JffCsharpTools.Application.Common;

namespace domain.interfaces.services;

public interface IProjectService
{
    Task<Result> ShowMenuAsync();
}