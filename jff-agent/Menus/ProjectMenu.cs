using Domain.Entities;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Agent.Menus;

public class ProjectMenu(IDefaultService defaultService, IGitHubCli gitHubCli, ILogger<ProjectMenu> logger)
{
    public async Task<Result> ShowMenuAsync()
    {
        var running = true;
        try
        {
            while (running)
            {
                Console.Clear();

                var option = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]Manage projects[/]")
                        .AddChoices(
                            "List projects",
                            "Add project",
                            "Delete project",
                            "Back"));

                switch (option)
                {
                    case "List projects":
                        await ListAsync();
                        break;

                    case "Add project":
                        await AddAsync();
                        break;

                    case "Delete project":
                        await DeleteAsync();
                        break;

                    case "Back":
                        running = false;
                        break;
                }
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            running = false;
            logger.LogError(ex, "Error while running the Projects menu.");
            return Result.Fail(ex);
        }
    }

    private async Task ListAsync()
    {
        var projects = await defaultService.Get<Project>(new Project());

        var projectList = projects.Value ?? new List<Project>();

        var table = new Table()
            .AddColumn("Name")
            .AddColumn("Repository")
            .AddColumn("Local path");

        foreach (var project in projectList)
        {
            table.AddRow(
                project.Name,
                project.RepositoryUrl,
                project.LocalPath);
        }

        AnsiConsole.Write(table);
        AnsiConsole.Prompt(
            new TextPrompt<string>("Press Enter to continue:")
                .AllowEmpty());
    }

    private async Task AddAsync()
    {
        var repositoriesResult = await AnsiConsole.Status()
            .StartAsync("Loading GitHub repositories...", _ => gitHubCli.ListRepositoriesAsync());

        if (!repositoriesResult.Success || repositoriesResult.Value is null)
        {
            var error = repositoriesResult.Error ?? "Unknown error.";
            logger.LogError("Failed to list GitHub repositories: {Error}", error);
            AnsiConsole.MarkupLine($"[red]Error loading repositories: {Markup.Escape(error)}[/]");
            Console.ReadKey();
            return;
        }

        if (repositoriesResult.Value.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No GitHub repositories found for the authenticated account.[/]");
            Console.ReadKey();
            return;
        }

        var repository = AnsiConsole.Prompt(
            new SelectionPrompt<Domain.DTOs.Integrations.GitHubRepositoryDto>()
                .Title("Select a GitHub repository:")
                .UseConverter(r => r.NameWithOwner)
                .AddChoices(repositoriesResult.Value));

        var project = new Project
        {
            Name = AnsiConsole.Ask("Name:", repository.NameWithOwner.Split('/').Last()),
            RepositoryUrl = repository.Url,
            LocalPath = AnsiConsole.Ask<string>("Local path:")
        };

        project.LocalPath = verifyAndCreateDirectory(project.LocalPath);

        var cloneResult = await AnsiConsole.Status()
            .StartAsync(
                "Cloning repository...",
                _ => gitHubCli.CloneRepositoryAsync(project.RepositoryUrl, project.LocalPath));

        if (!cloneResult.Success)
        {
            logger.LogError(
                "Failed to clone {RepositoryUrl} into {LocalPath}: {Error}",
                project.RepositoryUrl,
                project.LocalPath,
                cloneResult.Error);
            AnsiConsole.MarkupLine(
                $"[red]Error cloning repository: {Markup.Escape(cloneResult.Error ?? "Unknown error.")}[/]");
            AnsiConsole.MarkupLine("[yellow]The project was not saved.[/]");
            Console.ReadKey();
            return;
        }

        await defaultService.Create<Project>(0, project, false);
        await defaultService.SaveChanges();

        AnsiConsole.MarkupLine("[green]Project added![/]");
        Console.ReadKey();
    }

    private string verifyAndCreateDirectory(string path)
    {
        var dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "jff-agent-runner",
        "git-repositories");
        var directoryPath = Path.Combine(dataDirectory, path);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        return directoryPath;
    }

    private async Task DeleteAsync()
    {
        var projects = await defaultService.Get<Project>(new Project());

        if (projects.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No projects found.[/]");
            Console.ReadKey();
            return;
        }

        var project = AnsiConsole.Prompt(
            new SelectionPrompt<Project>()
                .Title("Select a project:")
                .UseConverter(p => p.Name)
                .AddChoices(projects.Value));

        var executions = await defaultService.Get<Execution>(f => f.ProjectId == project.Id);
        if (executions.Success && executions.Value.Any())
        {
            AnsiConsole.MarkupLine("[yellow]This project has executions.[/]");
            Console.ReadKey();
            return;
        }

        var confirm = AnsiConsole.Confirm(
            $"Delete project [red]{project.Name}[/]?");

        if (confirm)
        {
            await defaultService.DeleteByKey<Project, int>(0, project.Id, false);
            await defaultService.SaveChanges();
        }

        Console.ReadKey();
    }
}
