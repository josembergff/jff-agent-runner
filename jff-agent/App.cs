using Agent.Menus;
using domain.interfaces.services;
using JffCsharpTools.Application.Common;
using Spectre.Console;

public class App
{
    private readonly ProjectMenu projectMenu;
    private readonly AgentMenu agentMenu;
    private readonly ExecutionMenu executionMenu;

    public App(
        ProjectMenu projectMenu,
        AgentMenu agentMenu,
        ExecutionMenu executionMenu)
    {
        this.projectMenu = projectMenu;
        this.agentMenu = agentMenu;
        this.executionMenu = executionMenu;
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
                    .Title("Select one option:")
                    .AddChoices(
                        "Projects",
                        "Agents",
                        "Executions",
                        "Exit"));

            var result = new Result();

            switch (option)
            {
                case "Projects":
                    result = await projectMenu.ShowMenuAsync();
                    break;

                case "Agents":
                    result = await agentMenu.ShowMenuAsync();
                    break;

                case "Executions":
                    result = await executionMenu.ShowMenuAsync();
                    break;

                case "Exit":
                    running = false;
                    break;
            }

            if (!result.Success)
            {
                Console.WriteLine(result.Message);
                if (result.Messages.Any())
                {
                    Console.WriteLine("Error detail:");
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