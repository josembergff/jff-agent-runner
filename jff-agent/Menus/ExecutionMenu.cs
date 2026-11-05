
using System.Linq.Expressions;
using domain.interfaces.services;
using Domain.DTOs.Integrations;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Infrastructure;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;
using JffCsharpTools.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Spectre.Console;

public class ExecutionMenu(
    IDefaultService defaultService, ILogger<ExecutionMenu> logger, IExecutionService executionService, IGitHubCli gitHubCli)
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
                            "Add execution",
                            "List executions",
                            "List executions by agent",
                            "List executions by project",
                            "List running executions",
                            "Cancel execution",
                            "Delete execution",
                            "Back"));

                switch (option)
                {
                    case "Add execution":
                        await CreateAsync();
                        break;

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

                    case "Cancel execution":
                        await CancelAsync();
                        break;

                    case "Delete execution":
                        await RemoveAsync();
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
            logger.LogError(ex, "Error while running the Executions menu.");
            return Result.Fail(ex);
        }
    }


    private async Task CreateAsync()
    {
        // 1. Select a project
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

        // 2. Select an agent
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

        // 3. Enter the issue number
        var issueNumber = AnsiConsole.Ask<int>(
            "GitHub issue number:");

        // 4. Retrieve the issue
        GitHubIssueDto? issue;

        try
        {
            var issueResult = await gitHubCli.GetIssueAsync(
                project.RepositoryUrl,
                issueNumber);

            if (!issueResult.Success)
                throw new DomainException(issueResult.Message);
            issue = issueResult.Value;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error retrieving issue: {Markup.Escape(ex.Message)}[/]");

            Console.ReadKey(true);
            return;
        }

        if (issue is null)
        {
            AnsiConsole.MarkupLine("[red]Issue not found.[/]");
            Console.ReadKey(true);
            return;
        }

        // 5. Exibir detalhes
        var panel = new Panel(
            new Markup(
                $"[bold]{Markup.Escape(issue.Title)}[/]\n\n" +
                $"{Markup.Escape(issue.Body ?? "No description available.")}\n\n" +
                $"[grey]Status: {Markup.Escape(issue.State)}[/]\n" +
                $"URL: {Markup.Escape(issue.Url)}"))
        {
            Header = new PanelHeader($"Issue #{issue.Number}"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);

        AnsiConsole.MarkupLine(
            $"\nProject: [green]{Markup.Escape(project.Name)}[/]");

        AnsiConsole.MarkupLine(
            $"Agent: [green]{Markup.Escape(agent.Name)}[/]");

        // 6. Confirm the execution
        if (!AnsiConsole.Confirm("Do you want to start this execution?"))
            return;

        // 7. Create the execution
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Starting agent...", async ctx =>
                {
                    await executionService.CreateAsync(new Execution
                    {
                        ProjectId = project.Id,
                        AgentId = agent.Id,
                        IssueNumber = issue.Number,
                        Status = ExecutionStatusEnum.Running
                    });
                });
            AnsiConsole.MarkupLine(
                "[green]Execution started successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error starting execution: {Markup.Escape(ex.Message)}[/]");
        }

        Console.ReadKey(true);
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

    private async Task CancelAsync()
    {
        var executions = await defaultService.Get<Execution>(
            f => f.Status == ExecutionStatusEnum.Running, new string[] { "Project", "Agent" });

        if (executions.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No executions are currently running.[/]");
            Console.ReadKey(true);
            return;
        }

        var execution = AnsiConsole.Prompt(
            new SelectionPrompt<Execution>()
                .Title("Select an execution:")
                .UseConverter(e => $"Issue #{e.IssueNumber} - {e.Id}")
                .AddChoices(executions.Value));

        if (AnsiConsole.Confirm("Do you want to cancel this execution?"))
        {
            execution.Status = ExecutionStatusEnum.Cancelled;
            await defaultService.UpdateByKey<Execution, int>(0, execution, execution.Id, false);
            await defaultService.SaveChanges();
            AnsiConsole.MarkupLine("[green]Execution canceled.[/]");
        }

        Console.ReadKey(true);
    }

    private async Task RemoveAsync()
    {
        var executions = await defaultService.Get<Execution>(new Execution(), new string[] { "Project", "Agent" });

        if (executions.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No executions found.[/]");
            Console.ReadKey(true);
            return;
        }

        var execution = AnsiConsole.Prompt(
            new SelectionPrompt<Execution>()
                .Title("Select an execution:")
                .UseConverter(e => $"Issue #{e.IssueNumber} - {e.Status}")
                .AddChoices(executions.Value));

        if (execution.Status == ExecutionStatusEnum.Running)
        {
            AnsiConsole.MarkupLine(
                "[red]A running execution cannot be deleted. Cancel it first.[/]");

            Console.ReadKey(true);
            return;
        }

        if (AnsiConsole.Confirm("Do you want to permanently delete this execution?"))
        {
            await defaultService.DeleteByKey<Execution, int>(0, execution.Id, false);
            await defaultService.SaveChanges();
            AnsiConsole.MarkupLine("[green]Execution deleted.[/]");
        }

        Console.ReadKey(true);
    }
}
