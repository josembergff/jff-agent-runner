using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infra.Context;

public class JffAgentDbContext(DbContextOptions<JffAgentDbContext> options)
    : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Execution> Executions => Set<Execution>();
}