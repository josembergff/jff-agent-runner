# jff-agent-runner

A terminal app that delegates GitHub Issues to the AI coding agents already installed on your Linux server or VPS. You pick a repository, an agent and an issue. The agent implements the issue in its own `tmux` session and opens a Pull Request, and you follow its progress from the menu.

## How it works

jff-agent only orchestrates. It does not install, authenticate or configure anything.

1. You register **projects** (a GitHub repository and a local directory) and **agents** (a CLI command such as `claude`).
2. You start an **execution** by choosing a project, an agent and an issue.
3. jff-agent creates a fresh writable clone for the execution under `<project-local-path>/executions/execution-<id>`, then opens a detached `tmux` session named `jff-<id>` in that checkout. Codex receives its prompt at startup; other agents receive it as a bracketed paste once their terminal is ready. The prompt tells the agent to:
   - read the issue with `gh issue view <number> --json title,body,labels --jq '{title, body, labels}'`
   - follow the repository's `AGENT_INSTRUCTIONS.md` and `AGENTS.md`
   - work on a branch named `agent/<issue-number>`, created from `main`
   - implement the issue, then run the relevant checks and build
   - push the branch and open a Pull Request to `main` with `Closes #<issue-number>`
4. You follow the execution from the menu, or attach to the session to talk to the agent directly.

Registering a project creates only its directory; it does not clone the repository. Each execution clones into a separate subdirectory, so agents do not share a working tree and can create branches or commits independently. The checkout path is saved with the execution. Cancel, Complete and Delete close the tmux session and remove the checkout; failed starts also clean it up automatically. The execution record remains after Cancel or Complete, while Delete removes the record too.

The `jff-agent-runner-web` package provides the same project, agent and execution management through a Blazor web interface on port `3031`. Both applications use the same SQLite database and run GitHub CLI, tmux and configured agent commands as the Linux user that starts them.

The agent never merges: the prompt forbids merging and committing to `main`, so the Pull Request is left for you to review.

Every execution is instructed to complete implementation, validation, commit, push and Pull Request creation without pausing for routine confirmation. Agents should resolve ordinary ambiguity from the issue and repository conventions. They may ask only when an essential requirement cannot reasonably be inferred or a real external blocker prevents progress; they should finish independent work and state the blocker clearly.

To read issue details, agents are instructed to use the GitHub CLI JSON output for the title, body and labels:

```bash
gh issue view 34 --json title,body,labels --jq '{title, body, labels}'
```

## Security model

jff-agent asks for no keys, tokens or licenses, and it stores none. Every credential stays with the tool that owns it (`gh`, `claude`, `codex` and so on), and you set those up yourself on the server. jff-agent only asks an agent to implement an issue in a repository and shows you the status.

Agents run as the Linux user that started jff-agent; no root or `sudo` access is required. Codex is limited to workspace writes but gets outbound network access to read issues and push the Pull Request. Claude Code and OpenCode run with their automatic permission modes, which can execute commands with that Linux user's access. Issue text is untrusted input, so:

- Run jff-agent under a dedicated Linux user with access only to what the agents need.
- Delegate only issues you have read, especially in repositories where outsiders can open issues.
- Protect `main` with branch protection rules. The ban on merging is an instruction to the agent, not a technical restriction.
- Do not put secrets in an agent's **Arguments** field. It is stored as plain text in the local database and displayed in the agent list.

## Server requirements

Whichever way you install jff-agent, the server needs these:

| Requirement | Why it is needed |
| --- | --- |
| `git` | Used by the agents for branches, commits and pushes |
| [GitHub CLI](https://cli.github.com/) (`gh`) | Lists issues, and lets the agents read issues and open Pull Requests |
| `tmux` | Hosts each agent session |
| At least one agent CLI | Does the implementation, for example Claude Code, Codex or OpenCode (OpenCode can use a local Ollama model) |
| The SDKs of your target projects | Lets the agents build and test the code they change |

The last row depends on what you delegate. For an Angular frontend and a .NET backend, the server also needs Node.js and the .NET SDK.

## Installation

### Option 1: APT package (recommended)

For Ubuntu 24.04 on x64. Other distributions and versions have not been tested.

Add the repository once:

```bash
sudo install -d -m 0755 /etc/apt/keyrings
curl -fsSL https://josembergff.github.io/jff-agent-runner/public.asc \
  | sudo gpg --dearmor --yes -o /etc/apt/keyrings/jff-agent-runner.gpg
echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/jff-agent-runner.gpg] https://josembergff.github.io/jff-agent-runner ./" \
  | sudo tee /etc/apt/sources.list.d/jff-agent-runner.list
```

Then install the package. This also installs the .NET runtime, `git` and `tmux`, plus `gh` unless you turn off recommended packages:

```bash
sudo apt update
sudo apt install jff-agent-runner
```

Install the web interface alongside it when needed:

```bash
sudo apt install jff-agent-runner-web
```

### Option 1: systemd (recommended)

To keep the web app running after you disconnect from SSH and start it again after a reboot, configure a user-level `systemd` service. Run these commands as the Linux user that has authenticated `gh` and configured the agent CLIs:

```bash
mkdir -p ~/.config/systemd/user
cat > ~/.config/systemd/user/jff-agent-runner-web.service <<'EOF'
[Unit]
Description=jff-agent-runner web interface
After=network-online.target

[Service]
WorkingDirectory=%h
Environment=HOME=%h
Environment="PATH=%h/.local/bin:%h/.npm-global/bin:%h/.bun/bin:/usr/local/bin:/usr/bin:/bin"
ExecStart=/usr/bin/jff-agent-runner-web
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
EOF

sudo loginctl enable-linger "$USER"
systemctl --user daemon-reload
systemctl --user enable --now jff-agent-runner-web
```

If an agent CLI is installed in another directory, add that directory to the service's `PATH` above, or configure the agent with its full executable path. Check the service with `systemctl --user status jff-agent-runner-web`, and follow its logs with `journalctl --user -u jff-agent-runner-web -f`. Stop it with `systemctl --user disable --now jff-agent-runner-web`.

All commands above run as that Linux user. Only `sudo loginctl enable-linger "$USER"` requires `sudo`; it enables the user service to start at boot and remain active after logout.

### Option 2: tmux

To keep the web app running after disconnecting from SSH without configuring `systemd`, start it in a detached `tmux` session as the same Linux user:

```bash
tmux new-session -d -s jff-agent-runner-web 'jff-agent-runner-web'
```

The web app will be available at `http://<server>:3031`. Reattach to its terminal with:

```bash
tmux attach -t '=jff-agent-runner-web'
```

To detach in macOS Terminal, press **Control+B** (hold Control while pressing B), release both keys, then press lowercase **D**. Press them in sequence; do not use Command (⌘). If the shortcut does not work, your tmux prefix may be customized. You can close the SSH terminal and the tmux session will continue running, or open a second Terminal window, SSH to the server as the same user, and run:

```bash
tmux detach-client -s '=jff-agent-runner-web'
```

Check the session with `tmux ls`, or stop the web app with `tmux kill-session -t '=jff-agent-runner-web'`. A `tmux` session survives SSH logout but does not restart automatically after a server reboot.

On first access, create the web username and password; later visits require that login. The password is stored as a one-way hash in the shared `Configurations` table. Keep port 3031 behind a trusted network or HTTPS reverse proxy. For a temporary foreground run, execute `jff-agent-runner-web` directly; closing that terminal may stop the server.

To update later, run the commands above again, or update it with the rest of the system using `sudo apt update && sudo apt upgrade`. Your database is kept and migrated on the next run, and agent sessions that are already running are not affected.

To remove either app, run `sudo apt remove jff-agent-runner` or `sudo apt remove jff-agent-runner-web`. This keeps your database and project directories.

The repository only serves the latest version. To go back to an older one, download its `.deb` from [Releases](https://github.com/josembergff/jff-agent-runner/releases) and run `sudo apt install --allow-downgrades ./<file>.deb`.

### Option 2: From source

This needs the [.NET 10 SDK](https://dotnet.microsoft.com/download), and you install `git`, `gh` and `tmux` yourself. To update, run `git pull` in the clone.

```bash
git clone https://github.com/josembergff/jff-agent-runner.git
```

## Log in

Under the Linux user that will run jff-agent, log in to GitHub, including the project scope that the issue list needs:

```bash
gh auth login
gh auth refresh -s read:project
gh auth setup-git
```

Then check that each agent CLI starts and is logged in by running it once by hand.

## Start the app

If you installed the APT package, run this from any directory:

```bash
jff-agent-runner
```

If you installed from source, run this from the clone:

```bash
cd jff-agent-runner
dotnet run --project jff-agent
```

To start the web interface from source, run `dotnet run --project jff-agent-runner-web`; it listens on port `3031`.

Error log files are written to `logs/` under the directory you start the app from, so start it from one that you can write to.

The database is created and migrated automatically on startup. The app needs an interactive terminal, so run it from an SSH session.

Agent sessions run in their own `tmux` server, so they keep running if you close jff-agent or disconnect. They do not survive a server reboot.

## Usage

### 1. Add a project

Go to **Projects → Add project**, choose a repository from the list of repositories available to the authenticated `gh` account, then fill in:

| Field | Value |
| --- | --- |
| Name | Any display name |
| Repository | Selected from the authenticated GitHub account's repository list; its URL is filled in automatically |
| Local path | A relative name, such as `my-repo`, or an absolute path |

A relative path is placed under `~/jff-agent-runner/git-repositories/`.

jff-agent creates the directory and saves the project. The repository is cloned into a new `executions/execution-<id>` subdirectory each time an execution starts. The local path can already exist; existing files are preserved.

### 2. Add an agent

Go to **Agents → Add agent** and fill in:

| Field | Value |
| --- | --- |
| Name | Any display name |
| Command | The executable to start |
| Arguments | Optional flags, appended to the command |

Use an interactive CLI command; print or one-shot modes such as `claude -p` will not work. For Codex, Claude Code and OpenCode, jff-agent supplies the issue prompt at startup and enables unattended execution. Other CLIs receive the prompt as a bracketed paste and must be configured with their own unattended permission flags in **Arguments**.

The runner configures unattended execution for Codex, Claude Code and OpenCode. Other CLIs need their own unattended arguments. These are starting points, so check them against `--help` for your installed version:

| Agent | Command | Arguments |
| --- | --- | --- |
| Claude Code | `claude` | `--dangerously-skip-permissions` (added automatically if missing) |
| Codex | `codex` | `--model gpt-6-luna --config model_reasoning_effort=low` (approval, workspace-write sandbox, network access and per-checkout trust are set automatically) |
| OpenCode | `opencode` | `--model ollama/<model>` (add the model you use; `--auto` is added automatically) |

Codex trust is scoped to the execution checkout for that launch; the runner does not edit the user's Codex configuration or require `sudo`. Claude Code and OpenCode receive their prompt through native startup options. Future or other CLIs need their own unattended arguments configured here; their permission and first-run trust behavior is CLI-specific.

Executions run as the same Linux user that starts jff-agent. That user must be able to write to the project directory and must have authenticated `gh` and agent CLIs; no `sudo` access is needed to create or clean up execution checkouts.

### 3. Prepare the issue

The terminal menu only shows issues that are **open** and have the status **Ready** on a GitHub Project board. The web interface lists all **open** issues from the selected repository. The terminal menu requires the `read:project` scope; issues not on a board or in another column do not appear there.

### 4. Start an execution

Go to **Executions → Add execution**, select the project, agent and issue, review the details and confirm.

### 5. Follow the execution

| Menu option | What it does |
| --- | --- |
| List executions | Shows all executions, with optional filters by agent or project, or running only |
| View running | Shows a snapshot of a session's terminal, up to the last 100 lines of scrollback |
| Send Enter | Sends an Enter key to a selected running or pending tmux session |
| Complete execution | Closes the session, removes its checkout and marks the execution as completed |
| Cancel execution | Closes the session, removes its checkout and marks it as cancelled |
| Delete execution | Closes any remaining session, removes the checkout and deletes the execution record |

To watch live or answer a question from the agent, attach to its session from a shell. Replace `12` with the execution ID:

```bash
tmux attach -t jff-12
```

Detach with `Ctrl+B`, then `D`. Do not exit the agent unless you mean to end the session.

The prompt tells agents to proceed without routine questions and create the Pull Request. They should ask only when an essential requirement cannot reasonably be inferred or an external blocker prevents progress. Use **View terminal** to inspect output and **Send Enter** to press Enter in the selected session; on a prompt, this confirms the currently selected option. **Complete** closes the session and removes its checkout.

### Execution statuses

| Status | Meaning |
| --- | --- |
| Pending | The record is saved and the session is starting |
| Running | The agent received the prompt |
| Failed | The session could not start or the prompt could not be delivered |
| Cancelled | You cancelled it from the menu; its checkout is removed |
| Completed | You marked it complete; its tmux session and checkout were removed |

## Known limitations

- **An existing cached Blazor runtime can break the web UI after a package update.** If the issue selector does not load, clear the browser and reverse proxy/CDN cache for `/_framework/blazor.web.js` and reload the page.
- **Agent completion is not detected automatically.** Use **Complete execution** in the terminal or web interface to close the session, remove the checkout and mark the execution complete.
- **A completed execution record remains in the list.** Use **Delete execution** to remove the record after its checkout has already been cleaned up.
- **`main` is assumed.** The prompt always branches from and targets `main`, so repositories with another default branch are not supported yet.

## Data and logs

| What | Where |
| --- | --- |
| Database (SQLite), including an error log table named `logs` | `~/.config/jff-agent-runner/database.db` |
| Error log files | `logs/` under the directory you started the app from |
| Default location for project directories | `~/jff-agent-runner/git-repositories/` |
| Per-execution writable checkouts | An `executions/` subdirectory inside each project's local path |

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| An error, or no issues, when selecting an issue | Is the issue open and **Ready** on a Project board? Does `gh auth status` list the `read:project` scope? |
| Could not load GitHub repositories | Check that `gh auth status` shows an authenticated account with access to the repositories. |
| Could not create the execution checkout | Check that the app user can write inside the project's local path and that `gh auth status` grants access to the repository. |
| The agent starts, but `gh` or `git` commands fail | Check the app user's GitHub CLI authentication and network access from the execution environment. |
| "Agent command exited with status..." | The command or arguments are wrong, or the agent is not logged in. Run the same command by hand in the project directory. |
| The prompt sits unsubmitted or went into a dialog | Attach to the session and answer the dialog, then open the agent once by hand to clear its first-run prompts. |

## Development

The solution has four projects:

```text
jff-agent/          Terminal UI (Spectre.Console menus) and app startup
jff-agent-runner-web/ Blazor Server UI and web account authentication
jff-agent-domain/   Entities, interfaces, ExecutionService and the agent prompt
jff-agent-infra/    EF Core with SQLite, plus the gh and tmux process wrappers
```

```bash
dotnet build jff-agent-runner.slnx
dotnet tool restore                # installs dotnet-ef
./utils/new-migration.sh <Name>    # adds a migration and applies it to your local database
```

### Publishing a release

Pushing a tag such as `v1.2.3` runs [publish-apt.yml](.github/workflows/publish-apt.yml). It builds the `.deb`, tests the installation in a clean Ubuntu 24.04 container, publishes the signed APT repository to GitHub Pages and attaches the package to a GitHub Release.

```bash
git tag v1.2.3
git push origin v1.2.3
```

Tag a commit on `main`. Each tag needs a higher version than the last, or servers will not update.

To build packages without publishing them, run `./utils/build-deb.sh 1.2.3` and `./utils/build-web-deb.sh 1.2.3` on Debian or Ubuntu. Both files are written to `artifacts/`.

**One-time repository setup.** Run this once, before the first tag, on a machine with `gh` and `gpg`. It enables GitHub Pages and allows version tags to deploy to it, because the `github-pages` environment otherwise accepts only the default branch:

```bash
REPO=josembergff/jff-agent-runner

gh api --method POST "repos/$REPO/pages" -f build_type=workflow

gh api --method PUT "repos/$REPO/environments/github-pages" --input - <<'EOF'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
gh api --method POST "repos/$REPO/environments/github-pages/deployment-branch-policies" \
  -f name='v*' -f type=tag
```

Then create the signing key. It is generated in a temporary directory, so the only copy of the private key is the GitHub secret:

```bash
export GNUPGHOME="$(mktemp -d)"
gpg --batch --passphrase '' --quick-generate-key "jff-agent-runner APT repository" rsa4096 sign never
gpg --armor --export-secret-keys | gh secret set APT_GPG_PRIVATE_KEY --repo "$REPO"
rm -rf "$GNUPGHOME"
unset GNUPGHOME
```

If the key is ever compromised, repeat this step, publish a new tag, and run the `curl` command from [Option 1](#option-1-apt-package-recommended) again on each server.

See [AGENTS.md](AGENTS.md) for architecture details and contribution rules.

## License

[MIT](LICENSE)
