using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infra.Context;

public class JffAgentDbContext(DbContextOptions<JffAgentDbContext> options)
    : DbContext(options)
{
    public virtual DbSet<Project> Projects { get; set; }
    public virtual DbSet<Agent> Agents { get; set; }
    public virtual DbSet<Execution> Executions { get; set; }
}