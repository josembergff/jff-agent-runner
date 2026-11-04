
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure
// builder.Services.AddSingleton<IGitHubCli, GitHubCli>();
// builder.Services.AddSingleton<ITmuxExecutor, TmuxExecutor>();
// builder.Services.AddSingleton<IJsonStorage, JsonStorage>();

// Domain / Services
// builder.Services.AddTransient<ProjectService>();
// builder.Services.AddTransient<AgentService>();
// builder.Services.AddTransient<ExecutionService>();

// Application
builder.Services.AddTransient<App>();

using var host = builder.Build();

var app = host.Services.GetRequiredService<App>();

await app.RunAsync();
