using Agent.Menus;
using domain.interfaces.services;
using JffCsharpTools.Application.Common;
using Spectre.Console;

public class App
{
    private readonly ProjectMenu projectMenu;
    private readonly AgentMenu agentMenu;
    private readonly IExecutionService executionService;

    public App(
        ProjectMenu projectMenu,
        AgentMenu agentMenu,
        IExecutionService executionService)
    {
        this.projectMenu = projectMenu;
        this.agentMenu = agentMenu;
        this.executionService = executionService;
    }

    public async Task RunAsync()
    {
        var running = true;

        while (running)
        {
            Console.Clear();

            AnsiConsole.Write(
                new FigletText("JFF Agent Runner")
                    .Centered()
                    .Color(Color.Green));

            var option = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Selecione uma opção:")
                    .AddChoices(
                        "Projetos",
                        "Agentes",
                        "Executar Issue",
                        "Execuções em andamento",
                        "Histórico",
                        "Sair"));

            var result = new Result();

            switch (option)
            {
                case "Projetos":
                    result = await projectMenu.ShowMenuAsync();
                    break;

                case "Agentes":
                    result = await agentMenu.ShowMenuAsync();
                    break;

                case "Executar Issue":
                    result = await executionService.StartMenuAsync();
                    break;

                case "Execuções em andamento":
                    result = await executionService.ShowRunningAsync();
                    break;

                case "Histórico":
                    result = await executionService.ShowHistoryAsync();
                    break;

                case "Sair":
                    running = false;
                    break;
            }

            if (!result.Success)
            {
                Console.WriteLine(result.Message);
                if (result.Messages.Any())
                {
                    Console.WriteLine("Detalhes do erro:");
                    foreach (var message in result.Messages)
                    {
                        Console.WriteLine(message);
                    }
                }
                break;
            }
        }
    }
}