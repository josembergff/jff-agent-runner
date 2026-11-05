using domain.interfaces.services;
using Domain.Entities;
using JffCsharpTools.Application.Common;
using JffCsharpTools.Application.Interfaces;
using Spectre.Console;

public class ProjectMenu
{
    private readonly IDefaultService projectService;

    public ProjectMenu(IDefaultService projectService)
    {
        this.projectService = projectService;
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
                        .Title("[green]Gerenciar projetos[/]")
                        .AddChoices(
                            "Listar projetos",
                            "Cadastrar projeto",
                            "Excluir projeto",
                            "Voltar"));

                switch (option)
                {
                    case "Listar projetos":
                        await ListAsync();
                        break;

                    case "Cadastrar projeto":
                        await AddAsync();
                        break;

                    case "Excluir projeto":
                        await DeleteAsync();
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
            return Result.Fail(ex);
        }
    }

    private async Task ListAsync()
    {
        var projects = await projectService.Get<Project>();

        var table = new Table()
            .AddColumn("Nome")
            .AddColumn("Repositório")
            .AddColumn("Diretório");

        foreach (var project in projects.Value)
        {
            table.AddRow(
                project.Name,
                project.RepositoryUrl,
                project.LocalPath);
        }

        AnsiConsole.Write(table);
        AnsiConsole.Prompt(
            new TextPrompt<string>("Pressione Enter para continuar:")
                .AllowEmpty());
    }

    private async Task AddAsync()
    {
        var project = new Project
        {
            Name = AnsiConsole.Ask<string>("Nome:"),
            RepositoryUrl = AnsiConsole.Ask<string>("URL do GitHub:"),
            LocalPath = AnsiConsole.Ask<string>("Diretório local:")
        };

        await projectService.Create<Project>(0, project, false);

        AnsiConsole.MarkupLine("[green]Projeto cadastrado![/]");
        Console.ReadKey();
    }

    private async Task DeleteAsync()
    {
        var projects = await projectService.Get<Project>();

        if (projects.Value.Count() == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nenhum projeto cadastrado.[/]");
            Console.ReadKey();
            return;
        }

        var project = AnsiConsole.Prompt(
            new SelectionPrompt<Project>()
                .Title("Selecione o projeto:")
                .UseConverter(p => p.Name)
                .AddChoices(projects.Value));

        var confirm = AnsiConsole.Confirm(
            $"Excluir o projeto [red]{project.Name}[/]?");

        if (confirm)
            await projectService.DeleteByKey<Project, int>(0, project.Id, false);

        Console.ReadKey();
    }
}