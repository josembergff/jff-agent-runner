using domain.interfaces.services;
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

            switch (option)
            {
                case "Projetos":
                    await projectService.ShowMenuAsync();
                    break;

                case "Agentes":
                    await agentService.ShowMenuAsync();
                    break;

                case "Executar Issue":
                    await executionService.StartMenuAsync();
                    break;

                case "Execuções em andamento":
                    await executionService.ShowRunningAsync();
                    break;

                case "Histórico":
                    await executionService.ShowHistoryAsync();
                    break;

                case "Sair":
                    running = false;
                    break;
            }
        }
    }
}