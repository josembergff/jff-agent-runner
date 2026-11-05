
using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;
using JffCsharpTools.Application.Interfaces;
using Spectre.Console;

public class ExecutionMenu(
    IDefaultService defaultService)
{
    public async Task ShowMenuAsync()
    {
        var running = true;

        while (running)
        {
            Console.Clear();

            var option = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Execuções[/]")
                    .AddChoices(
                        "Listar execuções",
                        "Listar execuções por agente",
                        "Listar execuções por projeto",
                        "Listar execuções em andamento",
                        "Histórico",
                        "Voltar"));

            switch (option)
            {
                case "Listar execuções":
                    await ListAsync();
                    break;

                case "Listar execuções por agente":
                    await ListByAgentAsync();
                    break;

                case "Listar execuções por projeto":
                    await ListByProjectAsync();
                    break;

                case "Listar execuções em andamento":
                    await ListRunningAsync();
                    break;

                case "Histórico":
                    await ListAsync();
                    break;

                case "Voltar":
                    running = false;
                    break;
            }
        }
    }

    private async Task ListAsync(
        Expression<Func<Execution, bool>>? filter = null)
    {
        var executions = await defaultService.Get(filter, new string[] { "Project", "Agent" });

        var table = new Table()
            .AddColumn("ID")
            .AddColumn("Projeto")
            .AddColumn("Agente")
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

        AnsiConsole.MarkupLine("\n[grey]Pressione qualquer tecla para continuar.[/]");
        Console.ReadKey(true);
    }

    private async Task ListByAgentAsync()
    {
        var agents = await defaultService.Get<Domain.Entities.Agent>(new Domain.Entities.Agent());

        if (agents.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nenhum agente cadastrado.[/]");
            Console.ReadKey(true);
            return;
        }

        var agent = AnsiConsole.Prompt(
            new SelectionPrompt<Domain.Entities.Agent>()
                .Title("Selecione o agente:")
                .UseConverter(a => a.Name)
                .AddChoices(agents.Value));

        await ListAsync(f => f.AgentId == agent.Id);
    }

    private async Task ListByProjectAsync()
    {
        var projects = await defaultService.Get<Project>(new Project());

        if (projects.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nenhum projeto cadastrado.[/]");
            Console.ReadKey(true);
            return;
        }

        var project = AnsiConsole.Prompt(
            new SelectionPrompt<Project>()
                .Title("Selecione o projeto:")
                .UseConverter(p => p.Name)
                .AddChoices(projects.Value));

        await ListAsync(f => f.ProjectId == project.Id);
    }

    private async Task ListRunningAsync()
    {
        await ListAsync(f => f.Status == ExecutionStatusEnum.Running);
    }
}
