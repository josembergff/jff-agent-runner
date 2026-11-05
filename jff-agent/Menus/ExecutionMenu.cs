
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
            logger.LogError(ex, "Error while running the Projects menu.");
            return Result.Fail(ex);
        }
    }

    private async Task CreateAsync()
    {
        // Selecionar projeto e agente.
        // Informar número da Issue.
        // Consultar Issue pelo GitHubCli.
        // Exibir detalhes e solicitar confirmação.
        // Iniciar execução pelo ExecutionService.
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
            AnsiConsole.MarkupLine("[yellow]Nenhuma execução em andamento.[/]");
            Console.ReadKey(true);
            return;
        }

        var execution = AnsiConsole.Prompt(
            new SelectionPrompt<Execution>()
                .Title("Selecione a execução:")
                .UseConverter(e => $"Issue #{e.IssueNumber} - {e.Id}")
                .AddChoices(executions.Value));

        if (AnsiConsole.Confirm("Deseja cancelar esta execução?"))
        {
            execution.Status = ExecutionStatusEnum.Cancelled;
            await defaultService.UpdateByKey<Execution, int>(0, execution, execution.Id, false);
            await defaultService.SaveChanges();
            AnsiConsole.MarkupLine("[green]Execução cancelada.[/]");
        }

        Console.ReadKey(true);
    }

    private async Task RemoveAsync()
    {
        var executions = await defaultService.Get<Execution>(new Execution(), new string[] { "Project", "Agent" });

        if (executions.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nenhuma execução cadastrada.[/]");
            Console.ReadKey(true);
            return;
        }

        var execution = AnsiConsole.Prompt(
            new SelectionPrompt<Execution>()
                .Title("Selecione a execução:")
                .UseConverter(e => $"Issue #{e.IssueNumber} - {e.Status}")
                .AddChoices(executions.Value));

        if (execution.Status == ExecutionStatusEnum.Running)
        {
            AnsiConsole.MarkupLine(
                "[red]Não é possível excluir uma execução em andamento. Cancele primeiro.[/]");

            Console.ReadKey(true);
            return;
        }

        if (AnsiConsole.Confirm("Deseja excluir permanentemente esta execução?"))
        {
            await defaultService.DeleteByKey<Execution, int>(0, execution.Id, false);
            await defaultService.SaveChanges();
            AnsiConsole.MarkupLine("[green]Execução excluída.[/]");
        }

        Console.ReadKey(true);
    }
}
