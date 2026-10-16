using DragonKMG.Application;
using DragonKMG.Cli;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.LocalRuntime;
using DragonKMG.ModelGateway;
using DragonKMG.ToolGateway;

var db = Environment.GetEnvironmentVariable("DRAGONKMG_DB_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "dragonkmg.db");
Directory.CreateDirectory(Path.GetDirectoryName(db)!);
var store = new SqlitePlatformStore(db);
await store.InitializeAsync(default);
using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
var vault = new WindowsDpapiSecretVault(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "vault"));
var sourceSkills = Path.Combine(Directory.GetCurrentDirectory(), "skills");
var skillsRoot = Environment.GetEnvironmentVariable("DRAGONKMG_SKILLS_ROOT") ?? (Directory.Exists(sourceSkills) ? sourceSkills : Path.Combine(AppContext.BaseDirectory, "skills"));
var skillResolver = new SkillFileResolver(skillsRoot);
var service = new PlatformService(store, new OpenAiCompatibleGateway(httpClient), new CompositeSecretResolver(vault), skillResolver);
var fileContexts = new WorkspaceFileContextResolver();
var github = new GitHubIntegration(httpClient, new GitHubCredentialVault(vault), store, db);
var arguments = args.ToList();
if (arguments.Count == 0) { await TerminalControlPlane.OpenSessionAsync(store, service, "default", default, fileContexts, github); return; }
switch (arguments[0].ToLowerInvariant())
{
    case "tui": await TerminalControlPlane.OpenSessionAsync(store, service, arguments.ElementAtOrDefault(1) ?? "default", default, fileContexts, github); break;
    case "session" when arguments is [_, "list"]: Write(await store.ListTerminalSessionsAsync(default)); break;
    case "session" when arguments is [_, "open", var sessionName, ..]: await TerminalControlPlane.OpenSessionAsync(store, service, sessionName, default, fileContexts, github); break;
    case "hardware": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(HardwareProfiler.Probe())); break;
    case "connection" when arguments is [_, "list"]: Write(await store.ListConnectionsAsync(default)); break;
    case "connection" when arguments is [_, "add", var name, var endpoint, var secretEnv, ..]:
        var connection = new ProviderConnection(Guid.NewGuid(), name, ConnectionKind.OpenAiCompatible, new Uri(endpoint), $"env:{secretEnv}", true, TrustZone.Cloud, false, DateTimeOffset.UtcNow);
        ConnectionPolicy.Validate(connection); await store.SaveConnectionAsync(connection, default); Console.WriteLine(connection.Id); break;
    case "connection" when arguments is [_, "allow-cloud", var connectionId, ..]:
        var existingConnection = await store.FindConnectionAsync(Guid.Parse(connectionId), default) ?? throw new InvalidOperationException("Connection was not found.");
        await store.SaveConnectionAsync(existingConnection with { CloudEgressAllowed = true }, default); Console.WriteLine("Cloud egress enabled for this connection."); break;
    case "role" when arguments is [_, "add", var name, var instructions, ..]:
        var role = new AgentRole(Guid.NewGuid(), name, name, instructions, false, 1); await store.SaveRoleAsync(role, default); Console.WriteLine(role.Id); break;
    case "skill" when arguments is [_, "add", var key, var instructions, ..]:
        var skill = new SkillDefinition(Guid.NewGuid(), key, key, key, 1, true, SkillType.Instruction, instructions, "[]", "local"); await store.SaveSkillAsync(skill, default); Console.WriteLine(skill.Id); break;
    case "skill" when arguments is [_, "add-file", var key, var path, ..]:
        var fileSkill = new SkillDefinition(Guid.NewGuid(), key, key, key, 1, true, SkillType.Instruction, "", "[]", "local", "project-file", path);
        await skillResolver.ResolveAsync(fileSkill, default); await store.SaveSkillAsync(fileSkill, default); Console.WriteLine(fileSkill.Id); break;
    case "agent" when arguments is [_, "add", var name, var roleId, var connectionId, var model, ..]:
        var now = DateTimeOffset.UtcNow; var agent = new AgentDefinition(Guid.NewGuid(), name, name, Guid.Parse(roleId), string.Empty, true, model, Guid.Parse(connectionId), 1, now, now); await store.SaveAgentAsync(agent, default); Console.WriteLine(agent.Id); break;
    case "vault" when arguments is [_, "set", var vaultName, ..]: Console.Write("Secret: "); var secret = ReadSecret(); vault.Store(vaultName, secret); Console.WriteLine($"Stored vault:{vaultName}"); break;
    case "agent" when arguments is [_, "list"]: Write(await store.ListAgentsAsync(default)); break;
    case "agent" when arguments is [_, "run", var id, var input, ..]: Write(await service.RunAgentAsync(Guid.Parse(id), input, default)); break;
    default: Help(); Environment.ExitCode = 2; break;
}

static void Write<T>(T value) => Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
static string ReadSecret() { var value = new System.Text.StringBuilder(); ConsoleKeyInfo key; while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter) { if (key.Key == ConsoleKey.Backspace && value.Length > 0) value.Length--; else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar); } Console.WriteLine(); return value.ToString(); }
static void Help() => Console.WriteLine("DragonKMG: [no args] | tui [session-name] | session list | session open <name> | hardware | vault set <name> | connection list | connection add <name> <endpoint> <secret-env> | connection allow-cloud <connection-id> | role add <name> <instructions> | skill add <key> <instructions> | skill add-file <key> <relative-path.md> | agent add <name> <role-id> <connection-id> <model> | agent list | agent run <id> <input>");
