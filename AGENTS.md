# AGENTS.md

Guidance for AI coding agents working on this repository. For what the product does and how to use it, see [README.md](README.md).

## Project purpose

jff-agent-runner is a .NET 10 terminal app that runs on a Linux server. It delegates GitHub Issues to agent CLIs that are already installed and authenticated there, with each agent running in its own `tmux` session.

It is an orchestrator only. **It must never ask for, store or manage API keys, tokens or licenses.** GitHub and agent authentication belongs to `gh` and each agent CLI. The web interface has a separate local login; store only a password hash in `Configurations`.

## Commands

```bash
dotnet build jff-agent-runner.slnx     # build everything
dotnet run --project jff-agent         # run the app (needs an interactive terminal)
dotnet tool restore                    # install dotnet-ef from dotnet-tools.json
./utils/new-migration.sh <Name>        # add a migration AND apply it to the local database
./utils/build-deb.sh <x.y.z>           # build the .deb into artifacts/ (Debian or Ubuntu only)
./utils/build-web-deb.sh <x.y.z>      # build the Blazor web .deb
```

There is no test project yet, so validate changes by building. Exercise behavior by running the app, which needs `tmux` and an authenticated `gh`. Say so in your report when you could not run something.

The build currently reports many warnings (72 at the time of writing), so check for new ones rather than expecting a clean build.

## Architecture

Dependencies point one way: `jff-agent` → `jff-agent-infra` → `jff-agent-domain`. The app project also references the domain directly.

| Project | Contents |
| --- | --- |
| `jff-agent` | `Program.cs` (DI, logging, automatic migration), `App.cs` (main menu), `Menus/` (Spectre.Console screens) |
| `jff-agent-runner-web` | Blazor Server UI, cookie authentication and Razor Pages for initial account setup/login |
| `jff-agent-domain` | `Entities/`, `Enums/`, `DTOs/`, `Interfaces/`, `Services/ExecutionService.cs`, `Prompts/IssueImplementationPrompt.cs` |
| `jff-agent-infra` | `Context/` (EF Core DbContext), `Migrations/`, `GitHub/GitHubCli.cs`, `Tmux/TmuxExecutor.cs` |

Shared base types (`DefaultEntity`, `DefaultService`, `IDefaultRepository`, `Result`) come from the `jff-csharp-tools-10` NuGet package.

### Execution flow

`ExecutionMenu.CreateAsync` calls `ExecutionService.CreateAsync`, which:

1. Loads the project and agent, and ensures the project directory exists.
2. Saves the execution as `Pending`, then sets the session name `jff-<id>` from the generated ID.
3. Clones the repository into `<project.LocalPath>/executions/execution-<id>` as a writable, isolated checkout and saves that path on the execution.
4. Calls `TmuxExecutor.StartSessionAsync` in the checkout.
5. Passes the prompt from `IssueImplementationPrompt` through the native startup prompt options for Codex, Claude Code and OpenCode, or calls `TmuxExecutor.SendInstructionsAsync` for other agents. Codex trusts only the execution checkout for that process and uses workspace-write with network access so it can create the requested Pull Request.
6. Sets `Running`. On any exception, it sets `Failed`, stores the message in `SessionDetails` and attempts to remove the checkout.

## Conventions

- **Unattended issue execution:** follow [`AGENT_INSTRUCTIONS.md`](AGENT_INSTRUCTIONS.md) for every delegated issue. Make routine implementation decisions from the issue and repository context; do not pause to ask whether to proceed with implementation, commits, pushing the issue branch, or opening the requested Pull Request. Ask only when an essential requirement is genuinely impossible to infer or an external blocker prevents progress. If blocked, complete all independent work and report the exact blocker and the action needed.
- **Branch and Pull Request:** use `agent/<issue-number>` from `main`; commit and push only that branch and open a PR targeting `main` with `Closes #<issue-number>`. Never commit to `main` or merge the PR. Do not overwrite or discard pre-existing user changes.
- **Validation:** run relevant checks and report what actually ran and its outcome. Do not stop after editing to ask permission for the branch/commit/push/PR steps required by the task.

- **External processes:** add arguments through `ProcessStartInfo.ArgumentList`, never by building a shell string. This keeps user input from being parsed by a shell. The agent command is the one deliberate exception: `Command` and `Arguments` are joined and given to `tmux` as one command line.
- **tmux targets:** always use `ExactTarget(sessionName)`. A plain `-t` matches by prefix, so `jff-1` could hit `jff-12`.
- **Prompt delivery:** Codex, Claude Code and OpenCode accept the prompt at startup to avoid first-run dialogs and the TUI paste/submit race. Keep bracketed paste in `SendInstructionsAsync` for other agents; sending the prompt line by line could submit each line as its own message.
- **Issue details for agents:** when instructing an executor to read an issue, use `gh issue view <number> --json title,body,labels --jq '{title, body, labels}'` so it gets a consistent title, body and labels payload.
- **Errors:** infrastructure methods return `Result` or `Result<T>` rather than throwing. Each menu's `ShowMenuAsync` catches exceptions, logs them and returns `Result.Fail`.
- **Console output:** wrap dynamic text in `Markup.Escape(...)` when using Spectre markup. Issue titles and bodies often contain `[` and `]`, which otherwise break rendering or throw.
- **Language:** user-facing strings, code comments and documentation are in English.
- **Namespaces:** these are inconsistent today (`Domain.*`, `domain.interfaces.services`, `Agent.Menus`, and some global types). Match the surrounding file, and do not rename them as part of unrelated work.

## Database changes

- Entity changes need a migration in `jff-agent-infra/Migrations`. Migrations run automatically at app startup, so they reach every user's database on their next run from either app.
- Never edit a migration that has already been committed. Add a new one.
- `Execution.SesionAgentCommand` is misspelled and is a real database column. Renaming it requires a migration.
- `./utils/new-migration.sh` also updates the real database at `~/.config/jff-agent-runner/database.db`. Take care when experimenting with schema changes.

## Current behavior to keep in mind

These are known gaps, not bugs to fix in passing. Change them only when the task asks for it, and update the README's **Known limitations** section when you do.

- Agent completion is not detected automatically; use **Complete execution** to close the session, remove its checkout and mark it completed.
- **Cancel execution** closes the `tmux` session, removes its checkout and marks the execution cancelled. Complete and Delete also remove the checkout.
- `GitHubCli.ListOpenIssuesAsync` returns only issues with the status `Ready` on a GitHub Project board, which requires the `read:project` scope.
- `IssueImplementationPrompt` hardcodes `main` as the base branch.

## Keeping the docs accurate

`README.md` documents menu options, the prompt's branch and Pull Request rules, the issue filter, file locations and limitations. Update it in the same change when you alter any of these. `IssueImplementationPrompt` in particular is product behavior, so treat edits to it as carefully as code changes.

## Packaging

`utils/build-deb.sh` and `utils/build-web-deb.sh` declare package dependencies, and `.github/workflows/publish-apt.yml` publishes both packages when a `vX.Y.Z` tag is pushed. Update `Depends` when an app needs another shared framework or system tool. Both need `aspnetcore-runtime-10.0`.

## Repository rules

- Do not commit `bin/`, `obj/`, `logs/` or local database files.
- Do not add secrets, including in examples or test data.
- Keep changes within the scope of the task.
