using DragonKMG.Application;
using DragonKMG.Api;
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
var vaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "vault");
builder.Services.AddSingleton(new WindowsDpapiSecretVault(vaultPath));
builder.Services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
builder.Services.AddHttpClient<IModelGateway, OpenAiCompatibleGateway>(client => client.Timeout = TimeSpan.FromSeconds(90)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<IProviderCatalog, OpenAiCompatibleGateway>(client => client.Timeout = TimeSpan.FromSeconds(20)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ProviderConnectionService>();
builder.Services.AddScoped<PlatformService>();
builder.Services.AddScoped<SessionConversationService>();
var projectSkills = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "skills"));
var skillsRoot = Environment.GetEnvironmentVariable("DRAGONKMG_SKILLS_ROOT") ?? (Directory.Exists(projectSkills) ? projectSkills : Path.Combine(AppContext.BaseDirectory, "skills"));
builder.Services.AddSingleton<ISkillContentResolver>(new SkillFileResolver(skillsRoot));
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
app.MapProviderConnectionRoutes();
app.MapSessionConversationRoutes();
app.MapGet("/api/connections", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListConnectionsAsync(ct)));
app.MapGet("/api/settings/default-connection", async (IPlatformStore store, CancellationToken ct) => Results.Ok(new DefaultConnectionRequest(await store.GetDefaultConnectionAsync(ct))));
app.MapPut("/api/settings/default-connection", async (DefaultConnectionRequest value, IPlatformStore store, CancellationToken ct) =>
{
    if (value.ConnectionId is Guid id && await store.FindConnectionAsync(id, ct) is not { Enabled: true }) return Results.BadRequest("Select an enabled connection.");
    await store.SetDefaultConnectionAsync(value.ConnectionId, ct);
    return Results.Ok(value);
});
app.MapPost("/api/connections", async (ProviderConnection value, IPlatformStore store, CancellationToken ct) => { ConnectionPolicy.Validate(value); await store.SaveConnectionAsync(value, ct); return Results.Created($"/api/connections/{value.Id}", value); });
app.MapPut("/api/connections/{id:guid}", async (Guid id, ProviderConnection value, IPlatformStore store, CancellationToken ct) => { if (value.Id != id) return Results.BadRequest("Connection id must match the route."); ConnectionPolicy.Validate(value); await store.SaveConnectionAsync(value, ct); return Results.Ok(value); });
app.MapGet("/api/roles", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListRolesAsync(ct)));
app.MapPost("/api/roles", async (AgentRole value, IPlatformStore store, CancellationToken ct) => { await store.SaveRoleAsync(value, ct); return Results.Created($"/api/roles/{value.Id}", value); });
app.MapGet("/api/skills", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListSkillsAsync(ct)));
app.MapGet("/api/skills/files", () => Results.Ok(Directory.EnumerateFiles(skillsRoot, "SKILL.md", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden }).Take(200).Select(path => Path.GetRelativePath(skillsRoot, path).Replace('\\', '/')).Order().ToArray()));
app.MapGet("/api/skills/{id:guid}/content", async (Guid id, IPlatformStore store, ISkillContentResolver resolver, CancellationToken ct) =>
{
    var skill = await store.FindSkillAsync(id, ct);
    if (skill is null) return Results.NotFound();
    return Results.Ok(new { content = await resolver.ResolveAsync(skill, ct), skill.SourceRef });
});
app.MapPost("/api/skills", async (SkillDefinition value, IPlatformStore store, ISkillContentResolver resolver, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(value.Key) || string.IsNullOrWhiteSpace(value.Name)) return Results.BadRequest("A skill key and name are required.");
    if (value.SourceType is not ("user" or "inline" or "project-file" or "local-codex-skill")) return Results.BadRequest("Unsupported skill source type.");
    try
    {
        if (value.SourceType == "project-file") await resolver.ResolveAsync(value, ct);
        else if (string.IsNullOrWhiteSpace(value.InstructionContent)) return Results.BadRequest("Inline skill instructions are required.");
    }
    catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
    {
        return Results.BadRequest("Skill file is unavailable, outside the skills directory, or invalid UTF-8 Markdown.");
    }
    var safeValue = value.Type == SkillType.Imported ? value with { Enabled = false, TrustLevel = "untrusted" } : value;
    if (!await store.TrySaveSkillVersionAsync(safeValue, ct)) return Results.Conflict("Skill changed or key already exists. Refresh before saving.");
    return Results.Created($"/api/skills/{safeValue.Id}", safeValue);
});
app.MapGet("/api/agents", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAgentsAsync(ct)));
app.MapPost("/api/agents", async (AgentDefinition value, IPlatformStore store, CancellationToken ct) => { await store.SaveAgentAsync(value, ct); return Results.Created($"/api/agents/{value.Id}", value); });
app.MapPost("/api/council/default", async (DefaultCouncilProvisionRequest value, PlatformService service, CancellationToken ct) => Results.Ok(await service.ProvisionDefaultCouncilAsync(value, ct)));
app.MapGet("/api/council/default/workflow", () => Results.Ok(DefaultCouncil.Workflow));
app.MapGet("/api/council/default/templates", () => Results.Ok(DefaultCouncil.Agents));
app.MapPost("/api/council/default/run", async (RunRequest value, PlatformService service, CancellationToken ct) => Results.Ok(await service.RunDefaultCouncilAsync(value.Input, ct)));
app.MapGet("/api/workflows", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListWorkflowsAsync(ct)));
app.MapGet("/api/workflows/{id:guid}", async (Guid id, IPlatformStore store, CancellationToken ct) =>
{
    var workflow = await store.FindWorkflowAsync(id, ct); if (workflow is null) return Results.NotFound();
    return Results.Ok(new WorkflowDetail(workflow, await store.ListWorkflowStagesAsync(id, ct), await store.ListWorkflowEdgesAsync(id, ct)));
});
app.MapPut("/api/workflows/{id:guid}", async (Guid id, WorkflowDetail value, IPlatformStore store, CancellationToken ct) =>
{
    if (value.Workflow.Id != id) return Results.BadRequest("Workflow id must match the route.");
    await store.SaveWorkflowAsync(value.Workflow, value.Stages, value.Edges, ct); return Results.Ok(value);
});
app.MapPost("/api/workflows/{id:guid}/run", async (Guid id, RunRequest value, PlatformService service, CancellationToken ct) => Results.Ok(await service.RunWorkflowAsync(id, value.Input, ct)));
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
app.MapGet("/api/sessions", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListTerminalSessionsAsync(ct)));
app.MapPost("/api/sessions", async (TerminalSession value, IPlatformStore store, CancellationToken ct) => { await store.SaveTerminalSessionAsync(value, ct); return Results.Created($"/api/sessions/{value.Id}", value); });
app.MapGet("/api/sessions/{id:guid}/messages", async (Guid id, int? limit, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListSessionMessagesAsync(id, limit ?? 100, ct)));
app.MapGet("/api/approvals", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListApprovalsAsync(ct)));
app.MapPost("/api/approvals", async (ApprovalRequest value, IPlatformStore store, CancellationToken ct) => { if (value.ExpiresAt <= value.RequestedAt) return Results.BadRequest("Approval expiry must be after request time."); await store.SaveApprovalAsync(value, ct); return Results.Created($"/api/approvals/{value.Id}", value); });
app.MapPut("/api/approvals/{id:guid}", async (Guid id, ApprovalRequest value, IPlatformStore store, CancellationToken ct) => { if (id != value.Id || value.Status is ApprovalStatus.Pending) return Results.BadRequest("Resolution must target the route approval and be final."); var resolved = value with { ResolvedAt = DateTimeOffset.UtcNow }; await store.SaveApprovalAsync(resolved, ct); return Results.Ok(resolved); });
static bool TokensMatch(string expected, string supplied)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

app.Run();

public sealed record RunRequest(string Input);
public sealed record DefaultConnectionRequest(Guid? ConnectionId);
public sealed record WorkflowDetail(WorkflowDefinition Workflow, IReadOnlyList<WorkflowStage> Stages, IReadOnlyList<WorkflowEdge> Edges);
