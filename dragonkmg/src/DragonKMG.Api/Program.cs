using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.LocalRuntime;
using DragonKMG.ModelGateway;
using DragonKMG.ToolGateway;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var databasePath = Environment.GetEnvironmentVariable("DRAGONKMG_DB_PATH") ?? Path.Combine(AppContext.BaseDirectory, "dragonkmg.db");
builder.Services.AddSingleton<IPlatformStore>(_ => new SqlitePlatformStore(databasePath));
builder.Services.AddSingleton<ISecretResolver, EnvironmentSecretResolver>();
builder.Services.AddHttpClient<IModelGateway, OpenAiCompatibleGateway>(client => client.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddScoped<PlatformService>();
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DRAGONKMG_URL") ?? "http://127.0.0.1:5188");
var app = builder.Build();
await app.Services.GetRequiredService<IPlatformStore>().InitializeAsync(default);
await app.Services.GetRequiredService<IPlatformStore>().RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow, default);
var localApiToken = Environment.GetEnvironmentVariable("DRAGONKMG_LOCAL_API_TOKEN");
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { title = "An unexpected server error occurred." });
}));
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
    if (string.IsNullOrWhiteSpace(localApiToken)) { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; await context.Response.WriteAsJsonAsync(new { title = "Set DRAGONKMG_LOCAL_API_TOKEN before using the management API." }); return; }
    var supplied = context.Request.Headers["X-DragonKMG-Local-Token"].ToString();
    if (!TokensMatch(localApiToken, supplied)) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; await context.Response.WriteAsJsonAsync(new { title = "Local API authentication failed." }); return; }
    await next(context);
});
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api") || HttpMethods.IsGet(context.Request.Method)) { await next(context); return; }
    try { await next(context); }
    finally
    {
        try
        {
            var token = context.Request.Headers["X-DragonKMG-Local-Token"].ToString();
            var actor = $"local-token:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..12].ToLowerInvariant()}";
            var outcome = context.Response.StatusCode is >= 200 and < 400 ? "succeeded" : "failed";
            await context.RequestServices.GetRequiredService<IPlatformStore>().SaveAuditEventAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, actor, $"{context.Request.Method} {context.Request.Path}", context.Response.StatusCode, outcome), CancellationToken.None);
        }
        catch { /* Audit persistence must not replace the original API result. */ }
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", hardware = HardwareProfiler.Probe() }));
app.MapGet("/api/connections", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListConnectionsAsync(ct)));
app.MapPost("/api/connections", async (ProviderConnection value, IPlatformStore store, CancellationToken ct) => { ConnectionPolicy.Validate(value); await store.SaveConnectionAsync(value, ct); return Results.Created($"/api/connections/{value.Id}", value); });
app.MapPut("/api/connections/{id:guid}", async (Guid id, ProviderConnection value, IPlatformStore store, CancellationToken ct) => { if (value.Id != id) return Results.BadRequest("Connection id must match the route."); ConnectionPolicy.Validate(value); await store.SaveConnectionAsync(value, ct); return Results.Ok(value); });
app.MapGet("/api/roles", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListRolesAsync(ct)));
app.MapPost("/api/roles", async (AgentRole value, IPlatformStore store, CancellationToken ct) => { await store.SaveRoleAsync(value, ct); return Results.Created($"/api/roles/{value.Id}", value); });
app.MapGet("/api/skills", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListSkillsAsync(ct)));
app.MapPost("/api/skills", async (SkillDefinition value, IPlatformStore store, CancellationToken ct) => { var safeValue = value.Type == SkillType.Imported ? value with { Enabled = false, TrustLevel = "untrusted" } : value; await store.SaveSkillAsync(safeValue, ct); return Results.Created($"/api/skills/{safeValue.Id}", safeValue); });
app.MapGet("/api/agents", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAgentsAsync(ct)));
app.MapPost("/api/agents", async (AgentDefinition value, IPlatformStore store, CancellationToken ct) => { await store.SaveAgentAsync(value, ct); return Results.Created($"/api/agents/{value.Id}", value); });
app.MapPost("/api/council/default", async (DefaultCouncilProvisionRequest value, PlatformService service, CancellationToken ct) => Results.Ok(await service.ProvisionDefaultCouncilAsync(value, ct)));
app.MapGet("/api/council/default/workflow", () => Results.Ok(DefaultCouncil.Workflow));
app.MapPost("/api/council/default/run", async (RunRequest value, PlatformService service, CancellationToken ct) => Results.Ok(await service.RunDefaultCouncilAsync(value.Input, ct)));
app.MapGet("/api/agents/{id:guid}/effective-configuration", async (Guid id, PlatformService service, CancellationToken ct) => Results.Ok(await service.GetEffectiveConfigurationAsync(id, ct)));
app.MapGet("/api/agents/{id:guid}/skills", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAssignmentsAsync(id, ct)));
app.MapPut("/api/agents/{id:guid}/skills", async (Guid id, AgentSkillAssignment[] values, IPlatformStore store, CancellationToken ct) => { if (values.Any(x => x.AgentId != id)) return Results.BadRequest("Every assignment must belong to the route agent."); await store.ReplaceAssignmentsAsync(id, values, ct); return Results.NoContent(); });
app.MapGet("/api/agents/{id:guid}/tools", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListToolGrantsAsync(id, ct)));
app.MapPut("/api/agents/{id:guid}/tools", async (Guid id, AgentToolGrant[] values, IPlatformStore store, CancellationToken ct) => { if (values.Any(x => x.AgentId != id)) return Results.BadRequest("Every tool grant must belong to the route agent."); await store.ReplaceToolGrantsAsync(id, values, ct); return Results.NoContent(); });
app.MapGet("/api/agents/{id:guid}/tools/workspace-file-read", async (Guid id, string path, IPlatformStore store, CancellationToken ct) =>
{
    var granted = (await store.ListToolGrantsAsync(id, ct)).Any(grant => string.Equals(grant.ToolId, WorkspaceFileReader.ToolId, StringComparison.OrdinalIgnoreCase));
    if (!granted) return Results.Forbid();
    var workspaceRoot = Environment.GetEnvironmentVariable("DRAGONKMG_WORKSPACE_ROOT");
    if (string.IsNullOrWhiteSpace(workspaceRoot)) return Results.Problem("Set DRAGONKMG_WORKSPACE_ROOT to enable workspace file inspection.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var reader = new WorkspaceFileReader(workspaceRoot);
    return Results.Ok(await reader.ReadAsync(path, ct));
});
app.MapPost("/api/agents/{id:guid}/test", async (Guid id, RunRequest request, PlatformService service, CancellationToken ct) => Results.Ok(await service.RunAgentAsync(id, request.Input, ct)));
app.MapGet("/api/agents/{id:guid}/runs", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListRunsAsync(id, ct)));
app.MapGet("/api/audit-events", async (int? limit, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAuditEventsAsync(limit ?? 100, ct)));
static bool TokensMatch(string expected, string supplied)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

app.Run();

public sealed record RunRequest(string Input);
