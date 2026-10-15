using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.ToolGateway;

namespace DragonKMG.Cli;

internal static class TerminalControlPlane
{
    public static async Task RunAsync(IPlatformStore store, PlatformService service, CancellationToken cancellationToken)
    {
        var openFiles = new List<string>();
        AgentDefinition? selected = (await store.ListAgentsAsync(cancellationToken)).FirstOrDefault(agent => agent.Enabled);
        while (true)
        {
            await RenderAsync(store, selected, openFiles, cancellationToken);
            Console.Write("\nDragonKMG › ");
            var line = Console.ReadLine()?.Trim() ?? string.Empty;
            if (line is "exit" or "quit") return;
            var split = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = split.ElementAtOrDefault(0)?.ToLowerInvariant();
            var argument = split.ElementAtOrDefault(1) ?? string.Empty;
            try
            {
                switch (command)
                {
                    case "help": PrintHelp(); break;
                    case "agents": PrintAgents(await store.ListAgentsAsync(cancellationToken)); break;
                    case "use": selected = FindAgent(await store.ListAgentsAsync(cancellationToken), argument); break;
                    case "mode" when selected is not null: selected = selected with { Mode = ParseMode(argument), Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow }; await store.SaveAgentAsync(selected, cancellationToken); break;
                    case "model" when selected is not null: if (string.IsNullOrWhiteSpace(argument)) throw new ArgumentException("Provide a model ID."); selected = selected with { ModelId = argument, Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow }; await store.SaveAgentAsync(selected, cancellationToken); break;
                    case "open": await OpenAsync(openFiles, argument, cancellationToken); break;
                    case "files": Console.WriteLine(openFiles.Count == 0 ? "No session files are open." : string.Join(Environment.NewLine, openFiles)); break;
                    case "prompt" when selected is not null: Console.WriteLine((await service.RunAgentAsync(selected.Id, argument, cancellationToken)).Output); break;
                    case "clear": Console.Clear(); break;
                    default: Console.WriteLine("Use 'help', or select an agent first with 'use <id-or-name>'."); break;
                }
            }
            catch (Exception exception) { Console.WriteLine($"Blocked: {exception.Message}"); }
        }
    }

    private static async Task RenderAsync(IPlatformStore store, AgentDefinition? selected, IReadOnlyList<string> openFiles, CancellationToken ct)
    {
        Console.Clear(); Console.WriteLine("DragonKMG Terminal Control Plane  |  local-first, cross-platform"); Console.WriteLine(new string('=', 76));
        if (selected is null) { Console.WriteLine("Selected agent: none — use 'agents' then 'use <id-or-name>'."); return; }
        var connection = await store.FindConnectionAsync(selected.ConnectionId, ct);
        var skills = await store.ListAssignmentsAsync(selected.Id, ct);
        var grants = await store.ListToolGrantsAsync(selected.Id, ct);
        Console.WriteLine($"Agent: {selected.Name}  |  Mode: {selected.Mode}  |  Model: {selected.ModelId}");
        Console.WriteLine($"Connection: {connection?.Name ?? "missing"}  |  Trust: {connection?.TrustZone}  |  Cloud: {(connection?.CloudEgressAllowed == true ? "allowed" : "blocked")}");
        Console.WriteLine($"Skills: {skills.Count} assigned  |  Tool grants: {grants.Count}  |  Open files this session: {openFiles.Count}");
        Console.WriteLine("Type 'help' for commands. Prompts run only through the selected agent and its persisted policy.");
    }

    private static async Task OpenAsync(List<string> openFiles, string path, CancellationToken ct)
    {
        var root = Environment.GetEnvironmentVariable("DRAGONKMG_WORKSPACE_ROOT");
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Set DRAGONKMG_WORKSPACE_ROOT before opening workspace files.");
        var result = await new WorkspaceFileReader(root).ReadAsync(path, ct);
        if (!openFiles.Contains(result.RelativePath, StringComparer.OrdinalIgnoreCase)) openFiles.Add(result.RelativePath);
        Console.WriteLine($"Opened {result.RelativePath} ({result.Bytes} bytes)\n{result.Content}");
    }

    private static AgentDefinition FindAgent(IEnumerable<AgentDefinition> agents, string value) => agents.FirstOrDefault(agent => agent.Id.ToString().StartsWith(value, StringComparison.OrdinalIgnoreCase) || agent.Name.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("Agent was not found.");
    private static AgentMode ParseMode(string value) => Enum.TryParse<AgentMode>(value.Replace("-", string.Empty), true, out var mode) ? mode : throw new ArgumentException("Modes: execution, planonly, questiononly, review.");
    private static void PrintAgents(IEnumerable<AgentDefinition> agents) { foreach (var agent in agents) Console.WriteLine($"{agent.Id}  {agent.Name}  [{agent.Mode}]  {agent.ModelId}"); }
    private static void PrintHelp() => Console.WriteLine("agents | use <id-or-name> | mode <execution|planonly|questiononly|review> | model <model-id> | open <relative-path> | files | prompt <text> | clear | exit");
}
