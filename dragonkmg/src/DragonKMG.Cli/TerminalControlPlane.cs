using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.ToolGateway;

namespace DragonKMG.Cli;

internal static class TerminalControlPlane
{
    public static async Task OpenSessionAsync(IPlatformStore store, PlatformService service, string sessionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        var now = DateTimeOffset.UtcNow;
        var session = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase))
            ?? new TerminalSession(Guid.NewGuid(), sessionName, null, now, now);
        var openFiles = new List<string>();
        AgentDefinition? selected = session.SelectedAgentId is { } selectedId ? await store.FindAgentAsync(selectedId, cancellationToken) : null;
        selected ??= (await store.ListAgentsAsync(cancellationToken)).FirstOrDefault(agent => agent.Enabled);
        session = session with { SelectedAgentId = selected?.Id, LastOpenedAt = now };
        await store.SaveTerminalSessionAsync(session, cancellationToken);
        using var surface = new TerminalSurface { SessionName = session.Name };
        surface.Add("system", "Write below to begin · /help commands · /quick <text> for brief conversation without specialist skills\n" + (TerminalTextLayout.IsAvailable ? "Native Arabic shaping and bidirectional layout enabled." : "Arabic display requires the operating system ICU runtime."));
        foreach (var message in (await store.ListSessionMessagesAsync(session.Id, 100, cancellationToken)).Reverse())
            surface.Add(message.Role == "user" ? "you" : "assistant", message.Role + " · " + message.CreatedAt.ToLocalTime().ToString("HH:mm") + "\n" + message.Content);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                session = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Id == session.Id) ?? session;
                if (session.SelectedAgentId is Guid currentAgent) selected = await store.FindAgentAsync(currentAgent, cancellationToken);
                await UpdateContextAsync(surface, store, selected, openFiles, cancellationToken);
                var line = await surface.ReadAsync(cancellationToken);
                if (line is null) break;
                line = line.Trim();
                if (line.Length == 0) continue;
                var (command, argument) = TerminalInput.Parse(line);
                if (command is "exit" or "quit") break;
                try
                {
                    switch (command)
                    {
                        case "help": surface.Add("system", "/agents · /use <name> · /model <id> · /mode <execution|planonly|questiononly|review>\n/quick <text>: explicit brief chat, no specialist skill bodies or implementation\n/arabic · /open <relative-path> · /files · /chat · /report · /clear · /exit\nType any other text to send a full-profile prompt. Up/Down: draft history. PgUp/PgDn: transcript."); break;
                        case "arabic":
                            var companion = new Uri("http://127.0.0.1:5173/?session=" + session.Id + "#sessions");
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(companion.AbsoluteUri) { UseShellExecute = true });
                            surface.Add("system", "Arabic workspace opened in your browser. Same session and saved agents. Sign in with your local API token there if needed.\n" + companion.AbsoluteUri); break;
                        case "agents":
                            var agents = await store.ListAgentsAsync(cancellationToken);
                            surface.Add("system", agents.Count == 0 ? "No agents yet. Create a team in the dashboard." : string.Join("\n", agents.Select(agent => agent.Name + " [" + agent.Mode + "] · " + agent.ModelId + " · " + agent.Id.ToString()[..8])));
                            break;
                        case "use":
                            selected = FindAgent(await store.ListAgentsAsync(cancellationToken), argument);
                            session = session with { SelectedAgentId = selected.Id, LastOpenedAt = DateTimeOffset.UtcNow };
                            await store.SaveTerminalSessionAsync(session, cancellationToken);
                            surface.Add("system", "Selected " + selected.Name); break;
                        case "mode" when selected is not null:
                            selected = selected with { Mode = ParseMode(argument), Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow };
                            await store.SaveAgentAsync(selected, cancellationToken);
                            surface.Add("system", "Mode saved: " + selected.Mode); break;
                        case "model" when selected is not null:
                            ArgumentException.ThrowIfNullOrWhiteSpace(argument);
                            selected = selected with { ModelId = argument, Version = selected.Version + 1, UpdatedAt = DateTimeOffset.UtcNow };
                            await store.SaveAgentAsync(selected, cancellationToken);
                            surface.Add("system", "Model saved: " + selected.ModelId); break;
                        case "open":
                            var root = Environment.GetEnvironmentVariable("DRAGONKMG_WORKSPACE_ROOT");
                            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Set DRAGONKMG_WORKSPACE_ROOT before opening files.");
                            var file = await new WorkspaceFileReader(root).ReadAsync(argument, cancellationToken);
                            if (!openFiles.Contains(file.RelativePath, StringComparer.OrdinalIgnoreCase)) openFiles.Add(file.RelativePath);
                            surface.Add("system", "Opened " + file.RelativePath + " (" + file.Bytes + " bytes)\n" + file.Content); break;
                        case "files": surface.Add("system", openFiles.Count == 0 ? "No open files. /open <relative-path>" : string.Join("\n", openFiles)); break;
                        case "chat":
                            surface.Add("system", string.Join("\n\n", (await store.ListSessionMessagesAsync(session.Id, 100, cancellationToken)).Reverse().Select(message => message.Role + ": " + message.Content))); break;
                        case "report": surface.Add("system", "Report saved: " + await ExportReportAsync(store, session, selected, cancellationToken)); break;
                        case "clear": surface.Clear(); break;
                        default:
                        {
                            if (line.StartsWith('/') && command is not ("prompt" or "quick")) { surface.Add("error", "Unknown command. Use /help."); break; }
                            var latestSelection = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Id == session.Id);
                            if (latestSelection?.SelectedAgentId is Guid sharedAgent && sharedAgent != selected?.Id)
                            {
                                selected = await store.FindAgentAsync(sharedAgent, cancellationToken);
                                session = latestSelection;
                                surface.Add("system", "Shared session selected: " + selected?.Name);
                                await UpdateContextAsync(surface, store, selected, openFiles, cancellationToken);
                            }
                            if (selected is null) { surface.Add("error", "Select an agent first: /agents then /use <name>"); break; }
                            var input = command is "prompt" or "quick" ? argument : line;
                            ArgumentException.ThrowIfNullOrWhiteSpace(input);
                            surface.Add("you", "YOU\n" + input);
                            surface.Add("process", ConversationFastPath.Reply(input) is not null ? "LOCAL · instant greeting; no model call" : "REQUEST · " + selected.Name + " → " + selected.ModelId + (command == "quick" ? " · QUICK conversation" : " · FULL skills") + "\nValidating configuration; waiting for provider. No tests are being executed.");
                            var elapsed = System.Diagnostics.Stopwatch.StartNew();
                            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            var run = await surface.TrackAsync(new SessionConversationService(store, service).SendAsync(session.Id, selected.Id, input, requestCancellation.Token, command == "quick"), requestCancellation);
                            surface.Add("process", $"COMPLETED · {run.Id.ToString()[..8]} · {elapsed.Elapsed.TotalSeconds:F2}s · {run.EffectiveInstructions.Length:N0} instruction chars");
                            surface.Add("assistant", selected.Name + "\n" + (run.Output ?? "No textual output.")); break;
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    surface.Add("error", "CANCELLED · provider request interrupted. Your prompt is retained; retry when ready.");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Never echo upstream exceptions which can contain provider URLs or response data.
                    surface.Add("error", "BLOCKED · " + exception.GetType().Name + "\nCheck /agents, model, permissions and connection settings. Your prompt remains in session history; resend after correcting the issue.");
                }
            }
        }
        catch (OperationCanceledException) { surface.Add("system", "Session cancelled."); }
        finally
        {
            var latest = (await store.ListTerminalSessionsAsync(CancellationToken.None)).FirstOrDefault(value => value.Id == session.Id) ?? session;
            await store.SaveTerminalSessionAsync(latest with { LastOpenedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        }
    }

    private static async Task UpdateContextAsync(TerminalSurface surface, IPlatformStore store, AgentDefinition? selected, IReadOnlyList<string> files, CancellationToken ct)
    {
        if (selected is null) { surface.Context = "No agent selected · /agents then /use <name>"; return; }
        var connection = await store.FindConnectionAsync(selected.ConnectionId, ct);
        surface.Context = selected.Name + " · " + selected.Mode + " · " + selected.ModelId;
        var skills = await store.ListAssignmentsAsync(selected.Id, ct);
        var tools = await store.ListToolGrantsAsync(selected.Id, ct);
        surface.Resources = (connection?.Name ?? "Missing connection") + " · " + connection?.TrustZone + " · " + skills.Count(skill => skill.Enabled) + " skills · " + tools.Count + " tools · " + files.Count + " files";
    }

    private static AgentDefinition FindAgent(IEnumerable<AgentDefinition> agents, string value) => agents.FirstOrDefault(agent => agent.Id.ToString().StartsWith(value, StringComparison.OrdinalIgnoreCase) || agent.Name.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("Agent was not found.");
    private static async Task<string> ExportReportAsync(IPlatformStore store, TerminalSession session, AgentDefinition? agent, CancellationToken ct)
    {
        var messages = (await store.ListSessionMessagesAsync(session.Id, 500, ct)).Reverse();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonKMG", "reports"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{session.Id:N}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.md");
        var text = $"# DragonKMG session report\n\n- Session: {session.Name}\n- Agent: {agent?.Name ?? "none"}\n- Model: {agent?.ModelId ?? "none"}\n- Generated: {DateTimeOffset.UtcNow:O}\n\n## Conversation\n" + string.Join("\n\n", messages.Select(message => $"### {message.Role} — {message.CreatedAt:O}\n{message.Content}"));
        await File.WriteAllTextAsync(path, text, ct); return path;
    }
    private static AgentMode ParseMode(string value) => Enum.TryParse<AgentMode>(value.Replace("-", string.Empty), true, out var mode) && Enum.IsDefined(mode) ? mode : throw new ArgumentException("Modes: execution, planonly, questiononly, review.");
    private static void PrintAgents(IEnumerable<AgentDefinition> agents) { foreach (var agent in agents) Console.WriteLine($"{agent.Id}  {agent.Name}  [{agent.Mode}]  {agent.ModelId}"); }
    private static void PrintHelp() => Console.WriteLine("agents | use <id-or-name> | mode <execution|planonly|questiononly|review> | model <model-id> | open <relative-path> | files | prompt <text> | chat | report | clear | exit");
}
