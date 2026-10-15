using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.LocalRuntime;
using DragonKMG.ModelGateway;

var db = Environment.GetEnvironmentVariable("DRAGONKMG_DB_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "dragonkmg.db");
Directory.CreateDirectory(Path.GetDirectoryName(db)!);
var store = new SqlitePlatformStore(db);
await store.InitializeAsync(default);
using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
var service = new PlatformService(store, new OpenAiCompatibleGateway(httpClient), new EnvironmentSecretResolver());
var arguments = args.ToList();
if (arguments.Count == 0) { Help(); return; }
switch (arguments[0].ToLowerInvariant())
{
    case "hardware": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(HardwareProfiler.Probe())); break;
    case "connection" when arguments is [_, "list"]: Write(await store.ListConnectionsAsync(default)); break;
    case "connection" when arguments is [_, "add", var name, var endpoint, var secretEnv, ..]:
        var connection = new ProviderConnection(Guid.NewGuid(), name, ConnectionKind.OpenAiCompatible, new Uri(endpoint), $"env:{secretEnv}", true, TrustZone.Cloud, false, DateTimeOffset.UtcNow);
        ConnectionPolicy.Validate(connection); await store.SaveConnectionAsync(connection, default); Console.WriteLine(connection.Id); break;
    case "role" when arguments is [_, "add", var name, var instructions, ..]:
        var role = new AgentRole(Guid.NewGuid(), name, name, instructions, false, 1); await store.SaveRoleAsync(role, default); Console.WriteLine(role.Id); break;
    case "skill" when arguments is [_, "add", var key, var instructions, ..]:
        var skill = new SkillDefinition(Guid.NewGuid(), key, key, key, 1, true, SkillType.Instruction, instructions, "[]", "local"); await store.SaveSkillAsync(skill, default); Console.WriteLine(skill.Id); break;
    case "agent" when arguments is [_, "add", var name, var roleId, var connectionId, var model, ..]:
        var now = DateTimeOffset.UtcNow; var agent = new AgentDefinition(Guid.NewGuid(), name, name, Guid.Parse(roleId), string.Empty, true, model, Guid.Parse(connectionId), 1, now, now); await store.SaveAgentAsync(agent, default); Console.WriteLine(agent.Id); break;
    case "agent" when arguments is [_, "list"]: Write(await store.ListAgentsAsync(default)); break;
    case "agent" when arguments is [_, "run", var id, var input, ..]: Write(await service.RunAgentAsync(Guid.Parse(id), input, default)); break;
    default: Help(); Environment.ExitCode = 2; break;
}

static void Write<T>(T value) => Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
static void Help() => Console.WriteLine("DragonKMG: hardware | connection list | connection add <name> <endpoint> <secret-env> | role add <name> <instructions> | skill add <key> <instructions> | agent add <name> <role-id> <connection-id> <model> | agent list | agent run <id> <input>");
