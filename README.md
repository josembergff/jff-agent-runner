# jff-agent-runner

A terminal app that delegates GitHub Issues to the AI coding agents already installed on your Linux server or VPS. You pick a repository, an agent and an issue. The agent implements the issue in its own `tmux` session and opens a Pull Request, and you follow its progress from the menu.

## How it works

jff-agent only orchestrates. It does not install, authenticate or configure anything.

1. You register **projects** (a GitHub repository and a local clone) and **agents** (a CLI command such as `claude`).
2. You start an **execution** by choosing a project, an agent and an issue.
3. jff-agent opens a detached `tmux` session named `jff-<id>` in the project directory, starts the agent and pastes in a prompt that tells it to:
   - read the issue with `gh issue view`
   - work on a branch named `agent/<issue-number>`, created from `main`
   - implement the issue, then run the tests and the build
   - push the branch and open a Pull Request to `main` with `Closes #<issue-number>`
4. You follow the execution from the menu, or attach to the session to talk to the agent directly.

The agent never merges: the prompt forbids merging and committing to `main`, so the Pull Request is left for you to review.

## Security model

jff-agent asks for no keys, tokens or licenses, and it stores none. Every credential stays with the tool that owns it (`gh`, `claude`, `codex` and so on), and you set those up yourself on the server. jff-agent only asks an agent to implement an issue in a repository and shows you the status.

Agents still run with the full access of the Linux user that started jff-agent, and issue text is untrusted input to them. This matters most if you let agents skip their permission prompts, so:

- Run jff-agent under a dedicated Linux user with access only to what the agents need.
- Delegate only issues you have read, especially in repositories where outsiders can open issues.
- Protect `main` with branch protection rules. The ban on merging is an instruction to the agent, not a technical restriction.
- Do not put secrets in an agent's **Arguments** field. It is stored as plain text in the local database and displayed in the agent list.

## Server requirements

Install these on the server, and authenticate them under the Linux user that will run jff-agent:

| Requirement | Why it is needed |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Builds and runs jff-agent |
| `git` | Used by the agents for branches, commits and pushes |
| [GitHub CLI](https://cli.github.com/) (`gh`) | Lists issues, and lets the agents read issues and open Pull Requests |
| `tmux` | Hosts each agent session |
| At least one agent CLI | Does the implementation, for example Claude Code, Codex or OpenCode (OpenCode can use a local Ollama model) |
| The SDKs of your target projects | Lets the agents build and test the code they change |

The last row depends on what you delegate. For an Angular frontend and a .NET backend, the server also needs Node.js and the .NET SDK.

Log in to GitHub, including the project scope that the issue list needs:

```bash
gh auth login
gh auth refresh -s read:project
gh auth setup-git
```

Then check that each agent CLI starts and is logged in by running it once by hand.

## Installation

```bash
git clone https://github.com/josembergff/jff-agent-runner.git
cd jff-agent-runner
dotnet run --project jff-agent
```

The database is created and migrated automatically on startup. The app needs an interactive terminal, so run it from an SSH session.

Agent sessions run in their own `tmux` server, so they keep running if you close jff-agent or disconnect. They do not survive a server reboot.

## Usage

### 1. Add a project

Go to **Projects → Add project** and fill in:

| Field | Value |
| --- | --- |
| Name | Any display name |
| GitHub URL | `https://github.com/owner/repo` or `owner/repo` |
| Local path | A relative name, such as `my-repo`, or an absolute path |

A relative path is placed under `~/jff-agent-runner/git-repositories/`.

jff-agent creates the directory and clones the repository into it with `gh repo clone`. The project is saved only if the clone succeeds. If it fails, the error is logged and shown, and nothing is saved.

The directory must be new or empty, so a folder that already holds a clone cannot be registered.

### 2. Add an agent

Go to **Agents → Add agent** and fill in:

| Field | Value |
| --- | --- |
| Name | Any display name |
| Command | The executable to start |
| Arguments | Optional flags, appended to the command |

The agent must start in **interactive mode**, because jff-agent types the prompt into its terminal. Print or one-shot modes such as `claude -p` will not work.

An agent that stops at a permission prompt waits there until you attach and answer it. For unattended runs, configure your agent to proceed on its own, after reading [Security model](#security-model). These are starting points, so check them against `--help` for your installed version:

| Agent | Command | Arguments |
| --- | --- | --- |
| Claude Code | `claude` | `--dangerously-skip-permissions` |
| Codex | `codex` | `--dangerously-bypass-approvals-and-sandbox` |
| OpenCode with Ollama | `opencode` | `--model ollama/<model>` (permissions are set in OpenCode's own config) |

Also open each agent once by hand inside a new project directory. Some CLIs show a first-run dialog, such as a folder trust prompt, and that dialog would receive the pasted prompt instead of the agent.

### 3. Prepare the issue

The issue list only shows issues that are **open** and have the status **Ready** on a GitHub Project board. An issue that is not on a board, or is in another column, will not appear.

### 4. Start an execution

Go to **Executions → Add execution**, select the project, agent and issue, review the details and confirm.

### 5. Follow the execution

| Menu option | What it does |
| --- | --- |
| List executions | Shows all executions, with optional filters by agent or project, or running only |
| View running | Shows a snapshot of a session's terminal, up to the last 100 lines of scrollback |
| Cancel execution | Marks a running execution as cancelled |
| Delete execution | Removes the record of an execution that is not running |

To watch live or answer a question from the agent, attach to its session from a shell. Replace `12` with the execution ID:

```bash
tmux attach -t jff-12
```

Detach with `Ctrl+B`, then `D`. Do not exit the agent unless you mean to end the session.

The prompt tells agents to stop and ask when an issue is ambiguous, so an execution that seems stuck may be waiting for you.

### Execution statuses

| Status | Meaning |
| --- | --- |
| Pending | The record is saved and the session is starting |
| Running | The agent received the prompt |
| Failed | The session could not start, the prompt could not be delivered, or **View running** found the session gone |
| Cancelled | You cancelled it from the menu |
| Completed | Defined, but not set by the app yet |

## Known limitations

- **Finished work still shows as Running.** jff-agent does not detect that an agent has finished. Check for the Pull Request on GitHub, or attach to the session.
- **A successful agent can be marked Failed.** If the session has closed when you open **View running**, the execution is marked Failed even if the agent opened its Pull Request first.
- **Cancelling does not stop the agent.** It only changes the status. To stop the agent, end its session with `tmux kill-session -t "=jff-12"`.
- **Finished sessions stay open.** An interactive agent keeps its session after finishing, so close old sessions with the same command. `tmux ls` lists them.
- **`main` is assumed.** The prompt always branches from and targets `main`, so repositories with another default branch are not supported yet.

## Data and logs

| What | Where |
| --- | --- |
| Database (SQLite), including an error log table named `logs` | `~/.config/jff-agent-runner/database.db` |
| Error log files | `logs/` under the directory you started the app from |
| Default location for project directories | `~/jff-agent-runner/git-repositories/` |

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| An error, or no issues, when selecting an issue | Is the issue open and **Ready** on a Project board? Does `gh auth status` list the `read:project` scope? |
| "Error cloning repository" when adding a project | Is the URL right? Does `gh auth status` show you logged in with access to the repository? Is the local directory new or empty? |
| "Project directory does not exist" | The local directory was removed. Recreate and clone it. |
| The agent starts, but `gh` or `git` commands fail | The directory is empty or is not a clone of the repository. |
| "Agent command exited with status..." | The command or arguments are wrong, or the agent is not logged in. Run the same command by hand in the project directory. |
| The prompt sits unsubmitted or went into a dialog | Attach to the session and answer the dialog, then open the agent once by hand to clear its first-run prompts. |

## Development

The solution has three projects:

```text
jff-agent/          Terminal UI (Spectre.Console menus) and app startup
jff-agent-domain/   Entities, interfaces, ExecutionService and the agent prompt
jff-agent-infra/    EF Core with SQLite, plus the gh and tmux process wrappers
```

```bash
dotnet build jff-agent-runner.slnx
dotnet tool restore                # installs dotnet-ef
./utils/new-migration.sh <Name>    # adds a migration and applies it to your local database
```

See [AGENTS.md](AGENTS.md) for architecture details and contribution rules.

## License

[MIT](LICENSE)
