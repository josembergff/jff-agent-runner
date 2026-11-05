# AGENTS.md

Guidance for AI coding agents working on this repository. For what the product does and how to use it, see [README.md](README.md).

## Project purpose

jff-agent-runner is a .NET 10 terminal app that runs on a Linux server. It delegates GitHub Issues to agent CLIs that are already installed and authenticated there, with each agent running in its own `tmux` session.

It is an orchestrator only. **It must never ask for, store or manage API keys, tokens or licenses.** Authentication belongs to `gh` and to each agent CLI. Do not add features that break this rule without an explicit request.

## Commands

```bash
dotnet build jff-agent-runner.slnx     # build everything
dotnet run --project jff-agent         # run the app (needs an interactive terminal)
dotnet tool restore                    # install dotnet-ef from dotnet-tools.json
./utils/new-migration.sh <Name>        # add a migration AND apply it to the local database
```

There is no test project yet, so validate changes by building. Exercise behavior by running the app, which needs `tmux` and an authenticated `gh`. Say so in your report when you could not run something.

The build currently reports many warnings (72 at the time of writing), so check for new ones rather than expecting a clean build.

## Architecture

Dependencies point one way: `jff-agent` → `jff-agent-infra` → `jff-agent-domain`. The app project also references the domain directly.

| Project | Contents |
| --- | --- |
| `jff-agent` | `Program.cs` (DI, logging, automatic migration), `App.cs` (main menu), `Menus/` (Spectre.Console screens) |
| `jff-agent-domain` | `Entities/`, `Enums/`, `DTOs/`, `Interfaces/`, `Services/ExecutionService.cs`, `Prompts/IssueImplementationPrompt.cs` |
| `jff-agent-infra` | `Context/` (EF Core DbContext), `Migrations/`, `GitHub/GitHubCli.cs`, `Tmux/TmuxExecutor.cs` |

Shared base types (`DefaultEntity`, `DefaultService`, `IDefaultRepository`, `Result`) come from the `jff-csharp-tools-10` NuGet package.

### Execution flow

`ExecutionMenu.CreateAsync` calls `ExecutionService.CreateAsync`, which:

1. Loads the project and agent, and checks that the project directory exists.
2. Saves the execution as `Pending`, then sets the session name `jff-<id>` from the generated ID.
3. Calls `TmuxExecutor.StartSessionAsync`.
4. Calls `TmuxExecutor.SendInstructionsAsync` with the prompt from `IssueImplementationPrompt`.
5. Sets `Running`. On any exception, it sets `Failed` and stores the message in `SessionDetails`.

## Conventions

- **External processes:** add arguments through `ProcessStartInfo.ArgumentList`, never by building a shell string. This keeps user input from being parsed by a shell. The agent command is the one deliberate exception: `Command` and `Arguments` are joined and given to `tmux` as one command line.
- **tmux targets:** always use `ExactTarget(sessionName)`. A plain `-t` matches by prefix, so `jff-1` could hit `jff-12`.
- **Prompt delivery:** keep the bracketed paste in `SendInstructionsAsync`. Sending the prompt line by line could submit each line as its own message.
- **Errors:** infrastructure methods return `Result` or `Result<T>` rather than throwing. Each menu's `ShowMenuAsync` catches exceptions, logs them and returns `Result.Fail`.
- **Console output:** wrap dynamic text in `Markup.Escape(...)` when using Spectre markup. Issue titles and bodies often contain `[` and `]`, which otherwise break rendering or throw.
- **Language:** user-facing strings, code comments and documentation are in English.
- **Namespaces:** these are inconsistent today (`Domain.*`, `domain.interfaces.services`, `Agent.Menus`, and some global types). Match the surrounding file, and do not rename them as part of unrelated work.

## Database changes

- Entity changes need a migration in `jff-agent-infra/Migrations`. Migrations run automatically at app startup, so they reach every user's database on their next run.
- Never edit a migration that has already been committed. Add a new one.
- `Execution.SesionAgentCommand` is misspelled and is a real database column. Renaming it requires a migration.
- `./utils/new-migration.sh` also updates the real database at `~/.config/jff-agent-runner/database.db`. Take care when experimenting with schema changes.

## Current behavior to keep in mind

These are known gaps, not bugs to fix in passing. Change them only when the task asks for it, and update the README's **Known limitations** section when you do.

- `ExecutionStatusEnum.Completed` is never assigned, so finished work stays `Running`.
- **View running** marks an execution `Failed` when its session no longer exists, even if the agent finished successfully.
- **Cancel execution** only updates the database. It does not kill the `tmux` session.
- `ProjectMenu` creates the local directory but does not clone the repository.
- `GitHubCli.ListOpenIssuesAsync` returns only issues with the status `Ready` on a GitHub Project board, which requires the `read:project` scope.
- `IssueImplementationPrompt` hardcodes `main` as the base branch.

## Keeping the docs accurate

`README.md` documents menu options, the prompt's branch and Pull Request rules, the issue filter, file locations and limitations. Update it in the same change when you alter any of these. `IssueImplementationPrompt` in particular is product behavior, so treat edits to it as carefully as code changes.

## Repository rules

- Do not commit `bin/`, `obj/`, `logs/` or local database files.
- Do not add secrets, including in examples or test data.
- Keep changes within the scope of the task.
