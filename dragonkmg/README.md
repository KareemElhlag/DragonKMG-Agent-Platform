# DragonKMG local control plane

DragonKMG is a .NET 9, local-first control plane for governed agent configuration. It persists agent roles, skills, model connections, assignments, and run evidence in SQLite.

For paid-edition compatibility, upgrade invariants and the remaining commercial delivery checklist are maintained in [Commercial edition roadmap](docs/COMMERCIAL_EDITION_ROADMAP.md).

## Windows vault

On Windows, store a provider secret with `dragonkmg vault set provider_token`; the CLI requests the value without echoing it. Use `vault:provider_token` as the connection secret reference. The value is encrypted with Windows DPAPI for the current Windows user and is not placed in SQLite. `env:NAME` remains the portable fallback. macOS Keychain and Linux Secret Service adapters are planned and are not implemented yet.

On API startup, unfinished runs left in `Running` state by a process interruption are finalized as `Failed` with `InterruptedByRestart`. They are preserved in each agent's run history rather than silently discarded.

## Safe local startup

The management API is intentionally unavailable until a local token is configured. In PowerShell, set a new value for the current terminal session, then run the API:

```powershell
$env:DRAGONKMG_LOCAL_API_TOKEN = "create-a-long-random-local-token"
dotnet run --project src/DragonKMG.Api
```

The API listens on `http://127.0.0.1:5188` by default. It only exposes the health endpoint without authentication. Every `/api/*` request requires the same value in `X-DragonKMG-Local-Token`.

## Terminal control plane

Run `dotnet run --project src/DragonKMG.Cli -- tui` for the cross-platform interactive terminal UI. It uses standard console input/output, so it works in Windows Terminal, macOS/Linux terminals, SSH sessions, and mobile terminals that can run .NET.

For a Claude-style entry point, run the installed `dragonkmg` command with no arguments. It opens the persisted `default` session. Use `dragonkmg session open <name>` to enter or create a named session, and `dragonkmg session list` to see saved sessions. The selected agent is restored when that session opens again.

The status header always shows the selected agent, its mode, bound model and connection, cloud policy, assigned-skill count, tool-grant count, and files opened in the current session.

| Command | Purpose |
| --- | --- |
| `agents` | List agents with identifiers, modes, and models. |
| `use <id-or-name>` | Select the agent that receives the next prompt. |
| `mode <execution\|planonly\|questiononly\|review>` | Persistently switch the selected agent's behavior mode. |
| `model <model-id>` | Persistently change the selected agent's model binding. |
| `open <relative-path>` | Read and display a permitted workspace file; it is tracked as open for the session. |
| `files` | List files opened in the current terminal session. |
| `prompt <text>` | Run the prompt through the selected agent and its persisted policy. |
| `chat` | Show the persisted conversation for this session. |
| `report` | Export a Markdown session report with selected agent/model and conversation. |
| `help`, `clear`, `exit` | Terminal help and session controls. |

`open` requires `DRAGONKMG_WORKSPACE_ROOT` and keeps the same workspace-file safety restrictions as the API.

Prompts and returned agent outputs are persisted as session messages. Reports are written under the local DragonKMG reports directory and do not include secrets.

The Dashboard **Sessions** panel uses the same SQLite sessions and messages as the terminal. Open a session in the terminal, then select it in the dashboard to inspect its persisted chat history.

Start the dashboard in a second terminal:

```powershell
cd ui
npm run dev
```

Enter the local token in the dashboard header and refresh. It remains only for that browser tab session. Set `VITE_DRAGONKMG_API_URL` before starting Vite if the API address differs.

## Default multi-agent council

After creating a model connection, select it and a model ID in **Default multi-agent council**, then choose **Create default council**. The platform provisions three agents:

- **Execution Agent** implements the approved change.
- **Quality Review Agent** independently reviews correctness and operational UX.
- **Security Test Agent** performs defensive review and validates test evidence.

Each new council member receives the four local default skills. The operation is idempotent: it creates missing council members but does not overwrite existing agents or their skill choices. Assignments can be removed individually in **Agent skill assignments**, and custom skills can be created in **Skills library** then assigned to any agent.

Use **Run council workflow** to execute the stages in this order: Execution → Quality Review → Security Test Review. Each stage receives the original request plus bounded evidence from the stages before it. If a stage fails, later stages are not started.

The dashboard retrieves and displays the enforced workflow checklist before a run. Every stage is instructed to mark unknowns, blockers, and unchecked items explicitly; it must not claim a check passed without evidence.

## Tool permissions

Tool permissions are independent from skills. Add comma-separated tool IDs under **Agent skills and permissions** for a selected agent. A tool-backed skill is blocked before execution if any ID declared in its `RequiredToolIdsJson` is not explicitly granted. Grants for side-effecting tools retain an approval requirement; this local foundation does not execute host commands or untrusted MCP tools.

`workspace.file.read` is a real read-only tool. To use it, grant that exact tool ID to the agent and set `DRAGONKMG_WORKSPACE_ROOT` to an explicit workspace directory. Its authenticated API is `GET /api/agents/{agentId}/tools/workspace-file-read?path=relative/file.md`. It accepts only listed text extensions, enforces the workspace boundary, rejects links, binary data, and files larger than 1 MB.

## Agent modes

When creating an agent, choose one of four persisted modes: **Execution**, **Plan only — no code**, **Question only — no code**, or **Review only**. Plan, question, and review modes inject an execution guardrail into the runtime instructions: they may explain scope and evidence, but must not provide source code, patches, commands, or claim an implementation occurred.

## Audit trail

Mutating management API requests are stored in SQLite with timestamp, route/action, outcome, HTTP status, and a short one-way identifier derived from the local API token. Request bodies are deliberately excluded, so task prompts and secret values do not enter the audit trail. Read entries through authenticated `GET /api/audit-events?limit=100`.

## Configuration boundaries

- `DRAGONKMG_DB_PATH` optionally chooses the SQLite file location.
- `DRAGONKMG_URL` optionally changes the local listening address; retain a loopback address.
- Provider credentials are references only: `env:PROVIDER_SECRET_NAME`.
- Provider endpoints must use HTTPS, except explicit loopback development endpoints.
- Cloud egress starts disabled for every connection and must be explicitly enabled.

Do not expose this development control plane to a network. It is not a multi-user service and has no durable audit trail or production identity provider integration yet.
