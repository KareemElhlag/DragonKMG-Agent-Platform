using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.LocalRuntime;
using DragonKMG.ModelGateway;

var builder = WebApplication.CreateBuilder(args);
var databasePath = Environment.GetEnvironmentVariable("DRAGONKMG_DB_PATH") ?? Path.Combine(AppContext.BaseDirectory, "dragonkmg.db");
builder.Services.AddSingleton<IPlatformStore>(_ => new SqlitePlatformStore(databasePath));
builder.Services.AddSingleton<ISecretResolver, EnvironmentSecretResolver>();
builder.Services.AddHttpClient<IModelGateway, OpenAiCompatibleGateway>(client => client.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddScoped<PlatformService>();
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DRAGONKMG_URL") ?? "http://127.0.0.1:5188");
var app = builder.Build();
await app.Services.GetRequiredService<IPlatformStore>().InitializeAsync(default);

app.MapGet("/health", () => Results.Ok(new { status = "ok", hardware = HardwareProfiler.Probe() }));
app.MapGet("/api/connections", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListConnectionsAsync(ct)));
app.MapPost("/api/connections", async (ProviderConnection value, IPlatformStore store, CancellationToken ct) => { await store.SaveConnectionAsync(value, ct); return Results.Created($"/api/connections/{value.Id}", value); });
app.MapGet("/api/roles", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListRolesAsync(ct)));
app.MapPost("/api/roles", async (AgentRole value, IPlatformStore store, CancellationToken ct) => { await store.SaveRoleAsync(value, ct); return Results.Created($"/api/roles/{value.Id}", value); });
app.MapGet("/api/skills", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListSkillsAsync(ct)));
app.MapPost("/api/skills", async (SkillDefinition value, IPlatformStore store, CancellationToken ct) => { await store.SaveSkillAsync(value, ct); return Results.Created($"/api/skills/{value.Id}", value); });
app.MapGet("/api/agents", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAgentsAsync(ct)));
app.MapPost("/api/agents", async (AgentDefinition value, IPlatformStore store, CancellationToken ct) => { await store.SaveAgentAsync(value, ct); return Results.Created($"/api/agents/{value.Id}", value); });
app.MapGet("/api/agents/{id:guid}/effective-configuration", async (Guid id, PlatformService service, CancellationToken ct) => Results.Ok(await service.GetEffectiveConfigurationAsync(id, ct)));
app.MapGet("/api/agents/{id:guid}/skills", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListAssignmentsAsync(id, ct)));
app.MapPut("/api/agents/{id:guid}/skills", async (Guid id, AgentSkillAssignment[] values, IPlatformStore store, CancellationToken ct) => { if (values.Any(x => x.AgentId != id)) return Results.BadRequest("Every assignment must belong to the route agent."); await store.ReplaceAssignmentsAsync(id, values, ct); return Results.NoContent(); });
app.MapPost("/api/agents/{id:guid}/test", async (Guid id, RunRequest request, PlatformService service, CancellationToken ct) => Results.Ok(await service.RunAgentAsync(id, request.Input, ct)));
app.MapGet("/api/agents/{id:guid}/runs", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListRunsAsync(id, ct)));
app.Run();

public sealed record RunRequest(string Input);
