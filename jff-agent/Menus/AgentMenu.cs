
using JffCsharpTools.Application.Interfaces;
using Spectre.Console;
using Microsoft.Extensions.Logging;
using JffCsharpTools.Application.Common;
using Domain.Entities;

public class AgentMenu(IDefaultService defaultService, ILogger<AgentMenu> logger)
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
                        .Title("[green]Manage agents[/]")
                        .AddChoices(
                            "List agents",
                            "Add agent",
                            "Delete agent",
                            "Back"));

                switch (option)
                {
                    case "List agents":
                        await ListAsync();
                        break;

                    case "Add agent":
                        await CreateAsync();
                        break;

                    case "Delete agent":
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
            logger.LogError(ex, "Error while running the Agents menu.");
            return Result.Fail(ex);
        }
    }

    private async Task ListAsync()
    {
        var agents = await defaultService.Get<Domain.Entities.Agent>(new Domain.Entities.Agent());

        var table = new Table()
            .AddColumn("ID")
            .AddColumn("Name")
            .AddColumn("Command")
            .AddColumn("Arguments");

        foreach (var agent in agents.Value)
        {
            table.AddRow(
                agent.Id.ToString(),
                agent.Name,
                agent.Command,
                agent.Arguments);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue.[/]");
        Console.ReadKey(true);
    }

    private async Task CreateAsync()
    {
        var agent = new Domain.Entities.Agent
        {
            Name = AnsiConsole.Ask<string>("Name:"),
            Command = AnsiConsole.Ask<string>("Command:"),
            Arguments = AnsiConsole.Ask<string>(
                "Arguments (optional):", "")
        };

        await defaultService.Create<Domain.Entities.Agent>(0, agent, false);
        await defaultService.SaveChanges();

        AnsiConsole.MarkupLine("[green]Agent added successfully![/]");
        Console.ReadKey(true);
    }

    private async Task RemoveAsync()
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

        var executions = await defaultService.Get<Execution>(f => f.AgentId == agent.Id);
        if (executions.Success && executions.Value.Any())
        {
            AnsiConsole.MarkupLine("[yellow]This agent has executions.[/]");
            Console.ReadKey();
            return;
        }

        if (AnsiConsole.Confirm($"Delete agent [red]{agent.Name}[/]?"))
        {
            await defaultService.DeleteByKey<Domain.Entities.Agent, int>(0, agent.Id, false);
            await defaultService.SaveChanges();
            AnsiConsole.MarkupLine("[green]Agent deleted![/]");
        }

        Console.ReadKey(true);
    }
}
