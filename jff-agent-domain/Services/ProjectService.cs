using domain.interfaces.services;
using JffCsharpTools.Application.Services;
using JffCsharpTools.Domain.Interfaces.Repositories;

namespace Domain.Services;

public class ProjectService : DefaultService, IProjectService
{
    public ProjectService(IDefaultRepository repository) : base(repository)
    {

    }
}