using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.ToolGateway;

namespace DragonKMG.Cli;

internal static class TerminalControlPlane
{
    public static async Task OpenSessionAsync(IPlatformStore store, PlatformService service, string sessionName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionName)) throw new ArgumentException("Session name is required.");
        var now = DateTimeOffset.UtcNow;
        var session = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase)) ?? new TerminalSession(Guid.NewGuid(), sessionName, null, now, now);
        var openFiles = new List<string>();
        AgentDefinition? selected = session.SelectedAgentId is { } selectedId ? await store.FindAgentAsync(selectedId, cancellationToken) : null;
        selected ??= (await store.ListAgentsAsync(cancellationToken)).FirstOrDefault(agent => agent.Enabled);
        session = session with { SelectedAgentId = selected?.Id, LastOpenedAt = now };
        await store.SaveTerminalSessionAsync(session, cancellationToken);
        while (true)
        {
            await RenderAsync(store, selected, openFiles, session, cancellationToken);
            Console.Write("\nDragonKMG › ");
            var line = Console.ReadLine()?.Trim() ?? string.Empty;
            if (line is "exit" or "quit") { await store.SaveTerminalSessionAsync(session with { SelectedAgentId = selected?.Id, LastOpenedAt = DateTimeOffset.UtcNow }, cancellationToken); return; }
            var split = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = split.ElementAtOrDefault(0)?.ToLowerInvariant();
            var argument = split.ElementAtOrDefault(1) ?? string.Empty;
            try
            {
                switch (command)
                {
                    case "help": PrintHelp(); break;
                    case "agents": PrintAgents(await store.ListAgentsAsync(cancellationToken)); break;
                    case "use": selected = FindAgent(await store.ListAgentsAsync(cancellationToken), argument); session = session with { SelectedAgentId = selected.Id, LastOpenedAt = DateTimeOffset.UtcNow }; await store.SaveTerminalSessionAsync(session, cancellationToken); break;
                    case "mode" when selected is not null: selected = selected with { Mode = ParseMode(argument), Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow }; await store.SaveAgentAsync(selected, cancellationToken); break;
                    case "model" when selected is not null: if (string.IsNullOrWhiteSpace(argument)) throw new ArgumentException("Provide a model ID."); selected = selected with { ModelId = argument, Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow }; await store.SaveAgentAsync(selected, cancellationToken); break;
                    case "open": await OpenAsync(openFiles, argument, cancellationToken); break;
                    case "files": Console.WriteLine(openFiles.Count == 0 ? "No session files are open." : string.Join(Environment.NewLine, openFiles)); break;
                    case "prompt" when selected is not null:
                        await store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), session.Id, "user", argument, null, DateTimeOffset.UtcNow), cancellationToken);
                        var run = await service.RunAgentAsync(selected.Id, argument, cancellationToken);
                        await store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), session.Id, "assistant", run.Output ?? run.ErrorCode ?? string.Empty, run.Id, DateTimeOffset.UtcNow), cancellationToken);
                        Console.WriteLine(run.Output); break;
                    case "chat": await PrintChatAsync(store, session.Id, cancellationToken); break;
                    case "report": Console.WriteLine(await ExportReportAsync(store, session, selected, cancellationToken)); break;
                    case "clear": Console.Clear(); break;
                    default: Console.WriteLine("Use 'help', or select an agent first with 'use <id-or-name>'."); break;
                }
            }
            catch (Exception exception) { Console.WriteLine($"Blocked: {exception.Message}"); }
        }
    }

    private static async Task RenderAsync(IPlatformStore store, AgentDefinition? selected, IReadOnlyList<string> openFiles, TerminalSession session, CancellationToken ct)
    {
        Console.Clear(); Console.WriteLine($"DragonKMG Session: {session.Name}  |  local-first, cross-platform"); Console.WriteLine(new string('=', 76));
        if (selected is null) { Console.WriteLine("Selected agent: none — use 'agents' then 'use <id-or-name>'."); return; }
        var connection = await store.FindConnectionAsync(selected.ConnectionId, ct);
        var skills = await store.ListAssignmentsAsync(selected.Id, ct);
        var grants = await store.ListToolGrantsAsync(selected.Id, ct);
        Console.WriteLine($"Agent: {selected.Name}  |  Mode: {selected.Mode}  |  Model: {selected.ModelId}");
        Console.WriteLine($"Connection: {connection?.Name ?? "missing"}  |  Trust: {connection?.TrustZone}  |  Cloud: {(connection?.CloudEgressAllowed == true ? "allowed" : "blocked")}");
        var messages = await store.ListSessionMessagesAsync(session.Id, 1, ct);
        Console.WriteLine($"Skills: {skills.Count} assigned  |  Tool grants: {grants.Count}  |  Open files: {openFiles.Count}  |  Chat: {messages.Count}+ messages");
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
    private static async Task PrintChatAsync(IPlatformStore store, Guid sessionId, CancellationToken ct) { foreach (var message in (await store.ListSessionMessagesAsync(sessionId, 100, ct)).Reverse()) Console.WriteLine($"\n[{message.CreatedAt:HH:mm}] {message.Role}:\n{message.Content}"); }
    private static async Task<string> ExportReportAsync(IPlatformStore store, TerminalSession session, AgentDefinition? agent, CancellationToken ct)
    {
        var messages = (await store.ListSessionMessagesAsync(session.Id, 500, ct)).Reverse();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "reports"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{session.Name}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.md");
        var text = $"# DragonKMG session report\n\n- Session: {session.Name}\n- Agent: {agent?.Name ?? "none"}\n- Model: {agent?.ModelId ?? "none"}\n- Generated: {DateTimeOffset.UtcNow:O}\n\n## Conversation\n" + string.Join("\n\n", messages.Select(message => $"### {message.Role} — {message.CreatedAt:O}\n{message.Content}"));
        await File.WriteAllTextAsync(path, text, ct); return path;
    }
    private static AgentMode ParseMode(string value) => Enum.TryParse<AgentMode>(value.Replace("-", string.Empty), true, out var mode) ? mode : throw new ArgumentException("Modes: execution, planonly, questiononly, review.");
    private static void PrintAgents(IEnumerable<AgentDefinition> agents) { foreach (var agent in agents) Console.WriteLine($"{agent.Id}  {agent.Name}  [{agent.Mode}]  {agent.ModelId}"); }
    private static void PrintHelp() => Console.WriteLine("agents | use <id-or-name> | mode <execution|planonly|questiononly|review> | model <model-id> | open <relative-path> | files | prompt <text> | chat | report | clear | exit");
}
