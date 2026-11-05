
using JffCsharpTools.Application.Interfaces;
using Spectre.Console;
using Microsoft.Extensions.Logging;
using JffCsharpTools.Application.Common;

public class AgentMenu
{
    private readonly IDefaultService defaultService;
    private readonly ILogger<AgentMenu> logger;

    public AgentMenu(IDefaultService defaultService, ILogger<AgentMenu> logger)
    {
        this.defaultService = defaultService;
        this.logger = logger;
    }
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
                        .Title("[green]Gerenciar agentes[/]")
                        .AddChoices(
                            "Listar agentes",
                            "Cadastrar agente",
                            "Excluir agente",
                            "Voltar"));

                switch (option)
                {
                    case "Listar agentes":
                        await ListAsync();
                        break;

                    case "Cadastrar agente":
                        await CreateAsync();
                        break;

                    case "Excluir agente":
                        await RemoveAsync();
                        break;

                    case "Voltar":
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
            .AddColumn("Nome")
            .AddColumn("Comando")
            .AddColumn("Argumentos");

        foreach (var agent in agents.Value)
        {
            table.AddRow(
                agent.Id.ToString(),
                agent.Name,
                agent.Command,
                agent.Arguments);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[grey]Pressione qualquer tecla para continuar.[/]");
        Console.ReadKey(true);
    }

    private async Task CreateAsync()
    {
        var agent = new Domain.Entities.Agent
        {
            Name = AnsiConsole.Ask<string>("Nome:"),
            Command = AnsiConsole.Ask<string>("Comando:"),
            Arguments = AnsiConsole.Ask<string>(
                "Argumentos (opcional):", "")
        };

        await defaultService.Create<Domain.Entities.Agent>(0, agent, false);

        AnsiConsole.MarkupLine("[green]Agente cadastrado com sucesso![/]");
        Console.ReadKey(true);
    }

    private async Task RemoveAsync()
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

        if (AnsiConsole.Confirm($"Excluir o agente [red]{agent.Name}[/]?"))
        {
            await defaultService.DeleteByKey<Domain.Entities.Agent, int>(0, agent.Id, false);
            AnsiConsole.MarkupLine("[green]Agente excluído![/]");
        }

        Console.ReadKey(true);
    }
}
