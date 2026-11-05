using Domain.Entities;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Agent.Menus;

public class ProjectMenu(IDefaultService defaultService, ILogger<ProjectMenu> logger)
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
        var project = new Project
        {
            Name = AnsiConsole.Ask<string>("Name:"),
            RepositoryUrl = AnsiConsole.Ask<string>("GitHub URL:"),
            LocalPath = AnsiConsole.Ask<string>("Local path:")
        };

        await defaultService.Create<Project>(0, project, false);
        await defaultService.SaveChanges();

        AnsiConsole.MarkupLine("[green]Project added![/]");
        Console.ReadKey();
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