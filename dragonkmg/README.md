# DragonKMG local control plane

DragonKMG is a .NET 9, local-first control plane for governed agent configuration. It persists agent roles, skills, model connections, assignments, and run evidence in SQLite.

On API startup, unfinished runs left in `Running` state by a process interruption are finalized as `Failed` with `InterruptedByRestart`. They are preserved in each agent's run history rather than silently discarded.

## Safe local startup

The management API is intentionally unavailable until a local token is configured. In PowerShell, set a new value for the current terminal session, then run the API:

```powershell
$env:DRAGONKMG_LOCAL_API_TOKEN = "create-a-long-random-local-token"
dotnet run --project src/DragonKMG.Api
```

The API listens on `http://127.0.0.1:5188` by default. It only exposes the health endpoint without authentication. Every `/api/*` request requires the same value in `X-DragonKMG-Local-Token`.

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

## Configuration boundaries

- `DRAGONKMG_DB_PATH` optionally chooses the SQLite file location.
- `DRAGONKMG_URL` optionally changes the local listening address; retain a loopback address.
- Provider credentials are references only: `env:PROVIDER_SECRET_NAME`.
- Provider endpoints must use HTTPS, except explicit loopback development endpoints.
- Cloud egress starts disabled for every connection and must be explicitly enabled.

Do not expose this development control plane to a network. It is not a multi-user service and has no durable audit trail or production identity provider integration yet.
