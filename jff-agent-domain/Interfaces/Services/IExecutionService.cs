using Domain.Entities;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;

namespace domain.interfaces.services;

public interface IExecutionService : IDefaultService
{
    Task<Result> CreateAsync(Execution execution);
}