using System.Security.Claims;
using Domain.Entities;
using Domain.Interfaces.Infrastructure;
using domain.interfaces.services;
using Domain.Services;
using Infra.Context;
using Infra.Tmux;
using jff_agent_runner_web.Components;
using JffCsharpTools.Application.Interfaces;
using JffCsharpTools.Application.Services;
using JffCsharpTools.Domain.Interfaces.Repositories;
using JffCsharpTools10.Infra.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:3031");
var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "jff-agent-runner");
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "database.db");
builder.Services.AddDbContextFactory<JffAgentDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IDefaultRepository, DefaultRepository<JffAgentDbContext>>();
builder.Services.AddScoped<IDefaultService, DefaultService>();
builder.Services.AddScoped<IExecutionService, ExecutionService>();
builder.Services.AddSingleton<IGitHubCli, GitHubCli>();
builder.Services.AddSingleton<ITmuxExecutor, TmuxExecutor>();
builder.Services.AddScoped<IPasswordHasher<Configuration>, PasswordHasher<Configuration>>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "jff-agent-runner-web";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<JffAgentDbContext>().Database.MigrateAsync();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Account/Login");
}).RequireAuthorization();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
