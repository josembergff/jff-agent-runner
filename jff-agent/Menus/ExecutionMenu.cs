
using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Spectre.Console;

public class ExecutionMenu(
    IDefaultService defaultService, ILogger<ExecutionMenu> logger)
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
                        .Title("[green]Executions[/]")
                        .AddChoices(
                            "List executions",
                            "List executions by agent",
                            "List executions by project",
                            "List running executions",
                            "History",
                            "Back"));

                switch (option)
                {
                    case "List executions":
                        await ListAsync();
                        break;

                    case "List executions by agent":
                        await ListByAgentAsync();
                        break;

                    case "List executions by project":
                        await ListByProjectAsync();
                        break;

                    case "List running executions":
                        await ListRunningAsync();
                        break;

                    case "History":
                        await ListAsync();
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

    private async Task ListAsync(
        Expression<Func<Execution, bool>>? filter = null)
    {
        if (filter == null)
        {
            filter = f => 0 == 0;
        }
        var executions = await defaultService.Get(filter, new string[] { "Project", "Agent" }, false);

        var table = new Table()
            .AddColumn("ID")
            .AddColumn("Project")
            .AddColumn("Agent")
            .AddColumn("Issue")
            .AddColumn("Status");

        foreach (var execution in executions.Value)
        {
            table.AddRow(
                execution.Id.ToString(),
                execution.Project.Name,
                execution.Agent.Name,
                execution.IssueNumber.ToString(),
                execution.Status.ToString());
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[grey]Press any key to continue.[/]");
        Console.ReadKey(true);
    }

    private async Task ListByAgentAsync()
    {
        var agents = await defaultService.Get<Domain.Entities.Agent>(new Domain.Entities.Agent());

        if (agents.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No agents found.[/]");
            Console.ReadKey(true);
            return;
        }

        var agent = AnsiConsole.Prompt(
            new SelectionPrompt<Domain.Entities.Agent>()
                .Title("Select an agent:")
                .UseConverter(a => a.Name)
                .AddChoices(agents.Value));

        await ListAsync(f => f.AgentId == agent.Id);
    }

    private async Task ListByProjectAsync()
    {
        var projects = await defaultService.Get<Project>(new Project());

        if (projects.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No projects found.[/]");
            Console.ReadKey(true);
            return;
        }

        var project = AnsiConsole.Prompt(
            new SelectionPrompt<Project>()
                .Title("Select a project:")
                .UseConverter(p => p.Name)
                .AddChoices(projects.Value));

        await ListAsync(f => f.ProjectId == project.Id);
    }

    private async Task ListRunningAsync()
    {
        await ListAsync(f => f.Status == ExecutionStatusEnum.Running);
    }
}
