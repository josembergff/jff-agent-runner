
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
    IDefaultService defaultService, ILogger<ExecutionMenu> logger, IExecutionService executionService, IGitHubCli gitHubCli, ITmuxExecutor tmuxExecutor)
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
                            "View running",
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

                    case "View running":
                        await ViewRunningAsync();
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

        // 3. Select an issue number
        GitHubIssueDto? issue;

        try
        {
            var issueResult = await gitHubCli.ListOpenIssuesAsync(project.RepositoryUrl);

            if (!issueResult.Success)
            {
                logger.LogError(issueResult.Error, issueResult.StackTrace);
                throw new DomainException(issueResult.Error);
            }

            issue = AnsiConsole.Prompt(
                new SelectionPrompt<GitHubIssueDto>()
                    .Title("Select an issue:")
                    .UseConverter(a => a.Title)
                    .AddChoices(issueResult.Value));
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

        // 4. Exibir detalhes
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

        // 5. Confirm the execution
        if (!AnsiConsole.Confirm("Do you want to start this execution?"))
            return;

        // 6. Create the execution
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Starting agent...", async ctx =>
                {
                    var resultCreate = await executionService.CreateAsync(new Execution
                    {
                        ProjectId = project.Id,
                        AgentId = agent.Id,
                        IssueNumber = issue.Number,
                        Status = ExecutionStatusEnum.Running
                    });
                    if (!resultCreate.Success)
                    {
                        logger.LogError(resultCreate.Error, resultCreate.StackTrace);
                        throw new DomainException(resultCreate.Error);
                    }
                });
            AnsiConsole.MarkupLine(

                "[green]Execution started successfully![/]");
        }
        catch (Exception ex)
        {
            Console.Clear();
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
            .AddColumn("Status")
            .AddColumn("Session Name")
            .AddColumn("Session Details");

        foreach (var execution in executions.Value)
        {
            table.AddRow(
                execution.Id.ToString(),
                execution.Project.Name,
                execution.Agent.Name,
                execution.IssueNumber.ToString(),
                execution.Status.ToString(),
                execution.SessionName,
                execution.SessionDetails
                );
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

    private async Task ViewRunningAsync()
    {
        var executions = await defaultService.Get<Execution>(
            f => f.Status == ExecutionStatusEnum.Running || f.Status == ExecutionStatusEnum.Pending,
            new string[] { "Project", "Agent" });

        if (executions.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No executions are running or pending.[/]");
            Console.ReadKey(true);
            return;
        }

        var execution = AnsiConsole.Prompt(
            new SelectionPrompt<Execution>()
                .Title("Select an execution:")
                .UseConverter(e => Markup.Escape(
                    $"{e.SessionName} - {e.Project.Name} - {e.Agent.Name} - Issue #{e.IssueNumber} - {e.Status}"))
                .AddChoices(executions.Value));

        var hasSession = await tmuxExecutor.HasSessionAsync(execution.SessionName);

        if (!hasSession.Success)
        {
            logger.LogError(hasSession.Error, hasSession.StackTrace);
            AnsiConsole.MarkupLine(
                $"[red]Error checking session: {Markup.Escape(hasSession.Error ?? "Unknown error.")}[/]");
            Console.ReadKey(true);
            return;
        }

        if (!hasSession.Value)
        {
            execution.Status = ExecutionStatusEnum.Failed;
            execution.FinishedAt = DateTime.UtcNow;
            execution.UpdatedAt = DateTime.UtcNow;
            execution.SessionDetails = "Session was closed.";
            await defaultService.UpdateByKey<Execution, int>(0, execution, execution.Id, false);
            await defaultService.SaveChanges();

            AnsiConsole.MarkupLine(
                $"[yellow]Session {Markup.Escape(execution.SessionName)} was closed. Execution marked as failed.[/]");
            Console.ReadKey(true);
            return;
        }

        var output = await tmuxExecutor.GetOutputAsync(execution.SessionName);

        if (output.Success)
        {
            var content = output.Value.TrimEnd();

            AnsiConsole.Write(new Panel(new Text(content.Length > 0 ? content : "No output yet."))
            {
                Header = new PanelHeader(Markup.Escape($"{execution.SessionName} - {execution.Status}")),
                Border = BoxBorder.Rounded,
                Expand = true
            });
        }
        else
        {
            logger.LogError(output.Error, output.StackTrace);
            AnsiConsole.MarkupLine(
                $"[red]Error retrieving session output: {Markup.Escape(output.Error ?? "Unknown error.")}[/]");
        }

        AnsiConsole.MarkupLine("\n[grey]Press any key to continue.[/]");
        Console.ReadKey(true);
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
