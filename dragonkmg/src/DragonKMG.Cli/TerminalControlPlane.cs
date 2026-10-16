using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.ToolGateway;

namespace DragonKMG.Cli;

internal static class TerminalControlPlane
{
    public static async Task OpenSessionAsync(IPlatformStore store, PlatformService service, string sessionName, CancellationToken cancellationToken, IWorkspaceFileContextResolver? fileContexts = null, IGitHubIntegration? github = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        var now = DateTimeOffset.UtcNow;
        var session = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase))
            ?? new TerminalSession(Guid.NewGuid(), sessionName, null, now, now);
        var openFiles = new List<string>();
        var pendingFiles = new List<string>();
        var allowFileEgress = false;
        string? fileApprovalScope = null;
        var conversation = new SessionConversationService(store, service, fileContexts);
        AgentDefinition? selected = session.SelectedAgentId is { } selectedId ? await store.FindAgentAsync(selectedId, cancellationToken) : null;
        selected ??= (await store.ListAgentsAsync(cancellationToken)).FirstOrDefault(agent => agent.Enabled);
        session = session with { SelectedAgentId = selected?.Id, LastOpenedAt = now };
        await store.SaveTerminalSessionAsync(session, cancellationToken);
        using var surface = new TerminalSurface { SessionName = session.Name };
        surface.Add("system", "Choose a session team: /teams then /team use <name-or-id>. /team send <id> <prompt> overrides one request.\nWrite below to begin · /help commands\n" + (TerminalTextLayout.IsAvailable ? "Native Arabic shaping and bidirectional layout enabled." : "Arabic display requires the operating system ICU runtime."));
        foreach (var message in (await store.ListSessionMessagesAsync(session.Id, 100, cancellationToken)).Reverse())
            surface.Add(message.Role == "user" ? "you" : "assistant", message.Role + " · " + message.CreatedAt.ToLocalTime().ToString("HH:mm") + "\n" + message.Content);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                session = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Id == session.Id) ?? session;
                if (session.SelectedAgentId is Guid currentAgent) selected = await store.FindAgentAsync(currentAgent, cancellationToken);
                await UpdateContextAsync(surface, store, selected, openFiles, cancellationToken);
                var defaultTeamId = await store.GetSessionTeamAsync(session.Id, cancellationToken);
                var sessionTeam = (await store.ListTeamsAsync(cancellationToken)).FirstOrDefault(team => team.Id == defaultTeamId);
                surface.Resources += " · Team: " + (sessionTeam is null ? "NOT SET · /team use" : sessionTeam.Name + (sessionTeam.Enabled ? "" : " (disabled)"));
                var projectId = await store.GetSessionProjectAsync(session.Id, cancellationToken);
                var project = (await store.ListProjectsAsync(cancellationToken)).FirstOrDefault(value => value.Id == projectId);
                surface.Resources += " · Project: " + (project?.Name ?? "NOT SET · /project cwd") + " · " + pendingFiles.Count + " queued files";
                var line = await surface.ReadAsync(cancellationToken);
                if (line is null) break;
                line = line.Trim();
                if (line.Length == 0) continue;
                var (command, argument) = TerminalInput.Parse(line);
                if (line.StartsWith("@file ", StringComparison.OrdinalIgnoreCase)) { command = "file"; argument = line[6..]; }
                if (command is "team" or "project" or "model" or "mode" or "use" or "file" or "open") { allowFileEgress = false; fileApprovalScope = null; }
                if (command is "exit" or "quit") break;
                try
                {
                    switch (command)
                    {
                        case "github":
                            surface.Add("system", github is null ? "GitHub integration unavailable." : await TerminalGitHubCommands.ExecuteAsync(github, projectId, argument, cancellationToken));
                            break;
                        case "help": surface.Add("system", "/teams · /team use <name-or-id> · /team send <id> <prompt>\n/projects · /project cwd · /project use <name-or-id> · /project clear\n/file <absolute-or-project-relative-path> (or @file): preview and queue for next prompt · /file clear\n/allow-file-egress on|off: explicit permission for the NEXT request to send selected file contents to team model providers\n/agents · /use <name> · /model <id> · /mode <execution|planonly|questiononly|review>\n/quick <text>: brief chat, single-member team only\n/arabic · /open <path> · /files · /chat · /report · /clear · /exit\nFile access requires a project and workspace.file.read grants. Project selection grants no tools."); break;
                        case "projects":
                            var projects = await store.ListProjectsAsync(cancellationToken);
                            surface.Add("system", projects.Count == 0 ? "No projects. /project cwd selects the current directory." : string.Join("\n", projects.Select(value => value.Name + " · " + value.Id + "\n" + value.RootPath)));
                            break;
                        case "project":
                            surface.Add("system", await TerminalProjectCommands.ExecuteAsync(store, session.Id, argument, cancellationToken));
                            pendingFiles.Clear(); allowFileEgress = false;
                            break;
                        case "allow-file-egress":
                            if (argument is not ("on" or "off")) { surface.Add("error", "Use /allow-file-egress on|off. Default off."); break; }
                            allowFileEgress = argument == "on";
                            fileApprovalScope = allowFileEgress ? await conversation.GetFileApprovalScopeAsync(session.Id, null, string.Join("\n", pendingFiles.Select(path => "/file \"" + path + "\"")) + "\nRequest.", cancellationToken) : null;
                            surface.Add("system", allowFileEgress ? "NEXT request may transmit selected file contents to the team's model providers. Check the selected project/team and files before sending." : "File egress permission cleared.");
                            break;
                        case "teams":
                            var teams = await store.ListTeamsAsync(cancellationToken);
                            surface.Add("system", teams.Count == 0 ? "No teams yet. Create a named team in the dashboard." : string.Join("\n", teams.Select(team => team.Name + " · " + team.AgentIds.Count + " agents · " + (team.Enabled ? "enabled" : "disabled") + " · " + team.Id)));
                            break;
                        case "team" when !argument.StartsWith("send ", StringComparison.OrdinalIgnoreCase):
                            if (argument.Equals("clear", StringComparison.OrdinalIgnoreCase))
                            {
                                await SetSessionTeamAsync(store, session.Id, null, cancellationToken);
                                surface.Add("system", "Default team cleared. Choose a team before sending.");
                            }
                            else if (argument.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
                            {
                                var teamName = argument[4..].Trim();
                                var candidates = (await store.ListTeamsAsync(cancellationToken)).Where(value => value.Id.ToString().Equals(teamName, StringComparison.OrdinalIgnoreCase) || value.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase)).ToArray();
                                if (candidates.Length != 1 || !candidates[0].Enabled) { surface.Add("error", "Team unavailable or name is ambiguous. Use the full team ID shown by /teams."); break; }
                                var team = candidates[0];
                                await SetSessionTeamAsync(store, session.Id, team.Id, cancellationToken);
                                surface.Add("system", "Session default team saved: " + team.Name);
                            }
                            else surface.Add("system", "/teams · /team use <name-or-id> · /team clear · /team send <id> <prompt>");
                            break;
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
                        case "file":
                            if (argument.Equals("clear", StringComparison.OrdinalIgnoreCase)) { pendingFiles.Clear(); allowFileEgress = false; surface.Add("system", "Queued files cleared."); break; }
                            var requestedPath = argument.Trim().Trim('"');
                            if (requestedPath.Contains('\n') || requestedPath.Contains('\r') || requestedPath.Contains('"')) throw new ArgumentException("Invalid file path.");
                            if (pendingFiles.Count >= 4) { surface.Add("error", "At most four files per request. /file clear removes queued references."); break; }
                            var file = await conversation.PreviewFileAsync(session.Id, null, requestedPath, cancellationToken);
                            if (!pendingFiles.Contains(requestedPath, StringComparer.Ordinal)) pendingFiles.Add(requestedPath);
                            if (!openFiles.Contains(file.RelativePath, StringComparer.Ordinal)) openFiles.Add(file.RelativePath);
                            surface.Add("system", "LOCAL PREVIEW · " + file.RelativePath + " · " + file.Bytes + " bytes · SHA256 " + file.Sha256 + "\n" + file.Content + "\nQueued for next prompt. Content will be re-read and re-authorized before sending."); break;
                        case "files": surface.Add("system", openFiles.Count == 0 ? "No open files. /open <relative-path>" : string.Join("\n", openFiles)); break;
                        case "chat":
                            surface.Add("system", string.Join("\n\n", (await store.ListSessionMessagesAsync(session.Id, 100, cancellationToken)).Reverse().Select(message => message.Role + ": " + message.Content))); break;
                        case "report": surface.Add("system", "Report saved: " + await ExportReportAsync(store, session, selected, cancellationToken)); break;
                        case "clear": surface.Clear(); break;
                        default:
                        {
                            if (line.StartsWith('/') && command is not ("prompt" or "quick" or "team")) { surface.Add("error", "Unknown command. Use /help."); break; }
                            var latestSelection = (await store.ListTerminalSessionsAsync(cancellationToken)).FirstOrDefault(value => value.Id == session.Id);
                            if (latestSelection?.SelectedAgentId is Guid sharedAgent && sharedAgent != selected?.Id)
                            {
                                selected = await store.FindAgentAsync(sharedAgent, cancellationToken);
                                session = latestSelection;
                                surface.Add("system", "Shared session selected: " + selected?.Name);
                                await UpdateContextAsync(surface, store, selected, openFiles, cancellationToken);
                            }
                            var input = command is "prompt" or "quick" ? argument : line;
                            Guid? explicitTeam = null;
                            if (command == "team")
                            {
                                var parts = argument.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length != 3 || !Guid.TryParse(parts[1], out var teamId)) { surface.Add("error", "Use /team send <full-team-id> <prompt>"); break; }
                                explicitTeam = teamId;
                                input = parts[2];
                            }
                            ArgumentException.ThrowIfNullOrWhiteSpace(input);
                            if (pendingFiles.Count > 0) input = string.Join("\n", pendingFiles.Select(path => "/file \"" + path + "\"")) + "\n" + input;
                            if (explicitTeam is null && await store.GetSessionTeamAsync(session.Id, cancellationToken) is null)
                            {
                                surface.Add("error", "Which team should handle this request? Use /teams then /team use <name-or-id>, or /team send <id> <prompt>. No model was called.");
                                break;
                            }
                            surface.Add("you", "YOU\n" + input);
                            surface.Add("process", "TEAM REQUEST · sequential execution in saved member order. Waiting for actual server stages.");
                            var elapsed = System.Diagnostics.Stopwatch.StartNew();
                            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            AgentRun run;
                            var execution = new TerminalExecutionProgress(surface);
                            try { run = await surface.TrackAsync(conversation.SendTeamAsync(session.Id, explicitTeam, input, requestCancellation.Token, command == "quick", allowFileEgress, fileApprovalScope, execution), requestCancellation, execution.Flush); }
                            finally { execution.Flush(); allowFileEgress = false; fileApprovalScope = null; }
                            pendingFiles.Clear();
                            surface.Add("process", $"COMPLETED · {run.Id.ToString()[..8]} · {elapsed.Elapsed.TotalSeconds:F2}s total · {run.EffectiveInstructions.Length:N0} last-member instruction chars (not team token usage)");
                            surface.Add("assistant", "Team result\n" + (run.Output ?? "No textual output.")); break;
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    surface.Add("error", "CANCELLED · provider request interrupted. Your prompt is retained; retry when ready.");
                }
                catch (TeamRoutingException)
                {
                    surface.Add("error", "Choose an enabled team with available members: /teams then /team use <name-or-id>. No alternative team was selected.");
                }
                catch (FileContextException exception)
                {
                    allowFileEgress = false; fileApprovalScope = null;
                    surface.Add("error", "FILE BLOCKED · " + exception.Code + "\nCheck the session project, every team member's workspace.file.read grant and file path. Cloud file requests need /allow-file-egress on after queuing the files.");
                }
                catch (SessionBusyException)
                {
                    surface.Add("error", "This session already has a running request. Wait before changing its default team or sending again.");
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

    private static async Task SetSessionTeamAsync(IPlatformStore store, Guid sessionId, Guid? teamId, CancellationToken ct)
    {
        var owner = Guid.NewGuid();
        if (!await store.TryAcquireSessionLeaseAsync(sessionId, owner, DateTimeOffset.UtcNow.AddMinutes(1), ct)) throw new SessionBusyException();
        try
        {
            if (teamId is Guid id) await TeamPolicy.FindAvailableAsync(store, id, ct);
            await store.SetSessionTeamAsync(sessionId, teamId, ct);
        }
        finally { await store.ReleaseSessionLeaseAsync(sessionId, owner, CancellationToken.None); }
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
        var teamId = await store.GetSessionTeamAsync(session.Id, ct);
        var team = (await store.ListTeamsAsync(ct)).FirstOrDefault(value => value.Id == teamId);
        var text = $"# DragonKMG session report\n\n- Session: {session.Name}\n- Current default team: {team?.Name ?? "none"}\n- Current default team ID: {teamId?.ToString() ?? "none"}\n- Agent editor selection (not request routing): {agent?.Name ?? "none"}\n- Agent editor model: {agent?.ModelId ?? "none"}\n- Generated: {DateTimeOffset.UtcNow:O}\n\nTeam routing for historical requests is recorded in their conversation entries; the current default is not historical proof.\n\n## Conversation\n" + string.Join("\n\n", messages.Select(message => $"### {message.Role} — {message.CreatedAt:O}\n{message.Content}"));
        await File.WriteAllTextAsync(path, text, ct); return path;
    }
    private static AgentMode ParseMode(string value) => Enum.TryParse<AgentMode>(value.Replace("-", string.Empty), true, out var mode) && Enum.IsDefined(mode) ? mode : throw new ArgumentException("Modes: execution, planonly, questiononly, review.");
    private static void PrintAgents(IEnumerable<AgentDefinition> agents) { foreach (var agent in agents) Console.WriteLine($"{agent.Id}  {agent.Name}  [{agent.Mode}]  {agent.ModelId}"); }
    private static void PrintHelp() => Console.WriteLine("agents | use <id-or-name> | mode <execution|planonly|questiononly|review> | model <model-id> | open <relative-path> | files | prompt <text> | chat | report | clear | exit");
}
