
using domain.interfaces.services;
using Domain.Interfaces.Infrastructure;
using Domain.Services;
using Infra.Context;
using Infra.Tmux;
using JffCsharpTools.Application.Interfaces;
using JffCsharpTools.Application.Services;
using JffCsharpTools.Domain.Interfaces.Repositories;
using JffCsharpTools10.Infra.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Database
var dataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "jff-agent-runner");
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "database.db");
builder.Services.AddDbContext<JffAgentDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

// Infrastructure
builder.Services.AddSingleton<IGitHubCli, GitHubCli>();
builder.Services.AddSingleton<ITmuxExecutor, TmuxExecutor>();
builder.Services.AddSingleton<IDefaultRepository, DefaultRepository<JffAgentDbContext>>();

// Domain / Services
builder.Services.AddTransient<IDefaultService, DefaultService>();
builder.Services.AddTransient<IProjectService, ProjectService>();
builder.Services.AddTransient<IAgentService, AgentService>();
builder.Services.AddTransient<IExecutionService, ExecutionService>();

// Application
builder.Services.AddTransient<App>();
builder.Services.AddTransient<ProjectMenu>();

using var host = builder.Build();

// Context database
await using (var scope = host.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<JffAgentDbContext>();
    await dbContext.Database.MigrateAsync();
}

var app = host.Services.GetRequiredService<App>();

await app.RunAsync();
