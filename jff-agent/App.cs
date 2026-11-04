using domain.interfaces.services;
using JffCsharpTools.Application.Common;
using Spectre.Console;

public class App
{
    private readonly IProjectService projectService;
    private readonly IAgentService agentService;
    private readonly IExecutionService executionService;

    public App(
        IProjectService projectService,
        IAgentService agentService,
        IExecutionService executionService)
    {
        this.projectService = projectService;
        this.agentService = agentService;
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
                    result = await projectService.ShowMenuAsync();
                    break;

                case "Agentes":
                    result = await agentService.ShowMenuAsync();
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