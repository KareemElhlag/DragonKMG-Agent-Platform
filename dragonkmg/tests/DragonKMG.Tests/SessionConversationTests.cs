using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class SessionConversationTests
{
    [Fact]
    public async Task ExecutionProgressFollowsActualMemberCompletionAndPreservesRequestCorrelation()
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "progress", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var tracker = new SessionExecutionTracker();
        var id = Guid.NewGuid();
        var ignored = tracker.Observe(fixture.Session.Id, Guid.NewGuid());
        Assert.Null(tracker.Get(fixture.Session.Id));
        await fixture.Service(new OrderedGateway()).SendTeamAsync(fixture.Session.Id, team.Id, "Review the implementation", default, progress: tracker.Observe(fixture.Session.Id, id));
        var result = tracker.Get(fixture.Session.Id)!;
        Assert.Equal(id, result.RequestId);
        Assert.Equal(new[] { "validating_files", "running_agent", "agent_completed", "completed" }, result.Events.Select(value => value.Phase));
        Assert.Equal(fixture.Agent.ModelId, result.Events[1].ModelId);
        Assert.Equal(id, tracker.Get(fixture.Session.Id)!.RequestId); // Unreported/rejected requests do not replace active progress.
    }

    [Fact]
    public async Task ExecutionProgressRecordsFailureWithoutInventingCompletedMembers()
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "failed-progress", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var tracker = new SessionExecutionTracker();
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service(new FailedGateway()).SendTeamAsync(fixture.Session.Id, team.Id, "Review the implementation", default, progress: tracker.Observe(fixture.Session.Id, Guid.NewGuid())));
        var events = tracker.Get(fixture.Session.Id)!.Events;
        Assert.Equal("failed", events.Last().Phase);
        Assert.DoesNotContain(events, value => value.Phase is "completed" or "agent_completed");
    }
    [Fact]
    public async Task FileContextRequiresEveryMembersExplicitGrantBeforeReading()
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "files", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var now = DateTimeOffset.UtcNow;
        var project = new WorkspaceProject(Guid.NewGuid(), "files", Path.GetTempPath(), now, now);
        await fixture.Store.SaveProjectAsync(project, default);
        await fixture.Store.SetSessionProjectAsync(fixture.Session.Id, project.Id, default);
        var resolver = new CountingFileResolver();
        var service = fixture.Service(new FailedGateway(), resolver);
        var exception = await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default));
        Assert.Equal("FileAccessDenied", exception.Code);
        Assert.Equal(0, resolver.Calls);
        Assert.Empty(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default));
        await fixture.Store.ReplaceToolGrantsAsync(fixture.Agent.Id, [new(fixture.Agent.Id, "workspace.file.read", false, 1)], default);
        var second = fixture.Agent with { Id = Guid.NewGuid(), Name = "ungranted" };
        await fixture.Store.SaveAgentAsync(second, default);
        await fixture.Store.SaveTeamAsync(team with { Version = 2, AgentIds = [fixture.Agent.Id, second.Id] }, default);
        await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default));
        Assert.Equal(0, resolver.Calls);
    }

    [Fact]
    public async Task FileContextReachesModelButHistoryStoresOnlyOriginalDirectives()
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "files", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var now = DateTimeOffset.UtcNow;
        var project = new WorkspaceProject(Guid.NewGuid(), "files", Path.GetTempPath(), now, now);
        await fixture.Store.SaveProjectAsync(project, default);
        await fixture.Store.SetSessionProjectAsync(fixture.Session.Id, project.Id, default);
        await fixture.Store.ReplaceToolGrantsAsync(fixture.Agent.Id, [new(fixture.Agent.Id, "workspace.file.read", false, 1)], default);
        var gateway = new OrderedGateway();
        var resolver = new CountingFileResolver();
        await fixture.Service(gateway, resolver).SendTeamAsync(fixture.Session.Id, team.Id, "Review\n@file \"sample file.cs\"", default);
        Assert.Equal(1, resolver.Calls);
        Assert.Contains("SYNTHETIC_FILE_BODY", Assert.Single(gateway.Inputs));
        Assert.Contains("BEGIN UNTRUSTED FILE DATA", gateway.Inputs[0]);
        var messages = await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default);
        Assert.Contains(messages, message => message.Role == "user" && message.Content == "Review\n@file \"sample file.cs\"");
        Assert.DoesNotContain(messages.Where(message => message.Role == "user"), message => message.Content.Contains("SYNTHETIC_FILE_BODY"));
        Assert.Contains(await fixture.Store.ListRunsAsync(fixture.Agent.Id, default), run => run.Input.Contains("SYNTHETIC_FILE_BODY"));
    }

    [Theory]
    [InlineData(TrustZone.Cloud)]
    [InlineData(TrustZone.RemoteSelfHosted)]
    public async Task CloudFileEgressRequiresRequestConsentBeforeAnyReadButLocalPreviewDoesNot(TrustZone trustZone)
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "files", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var now = DateTimeOffset.UtcNow;
        await fixture.Store.SaveProjectAsync(new WorkspaceProject(Guid.NewGuid(), "files", Path.GetTempPath(), now, now), default);
        await fixture.Store.SetSessionProjectAsync(fixture.Session.Id, (await fixture.Store.ListProjectsAsync(default))[0].Id, default);
        await fixture.Store.ReplaceToolGrantsAsync(fixture.Agent.Id, [new(fixture.Agent.Id, "workspace.file.read", false, 1)], default);
        var connection = (await fixture.Store.FindConnectionAsync(fixture.Agent.ConnectionId, default))!;
        await fixture.Store.SaveConnectionAsync(connection with { TrustZone = trustZone, CloudEgressAllowed = true, Kind = ConnectionKind.OpenAiCompatible, Endpoint = new Uri("https://example.com/v1") }, default);
        var resolver = new CountingFileResolver(); var gateway = new OrderedGateway();
        var service = fixture.Service(gateway, resolver);
        var error = await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default));
        Assert.Equal("FileEgressApprovalRequired", error.Code);
        Assert.Equal(0, resolver.Calls);
        Assert.Empty(gateway.Inputs);
        await service.PreviewFileAsync(fixture.Session.Id, team.Id, "sample.cs", default);
        Assert.Equal(1, resolver.Calls);
        Assert.Empty(gateway.Inputs);
        var scope = await service.GetFileApprovalScopeAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default);
        Assert.Equal(1, resolver.Calls);
        // Consent is not a reusable boolean: changing any bound selection invalidates it.
        await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file different.cs", default, allowFileEgress: true, fileApprovalScope: scope));
        await fixture.Store.SaveAgentAsync(fixture.Agent with { ModelId = "changed-model", Version = 2 }, default);
        await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default, allowFileEgress: true, fileApprovalScope: scope));
        scope = await service.GetFileApprovalScopeAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default);
        await fixture.Store.SaveTeamAsync(team with { Version = 2 }, default);
        await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default, allowFileEgress: true, fileApprovalScope: scope));
        scope = await service.GetFileApprovalScopeAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default);
        var alternateProject = new WorkspaceProject(Guid.NewGuid(), "other", Path.GetTempPath(), now, now);
        await fixture.Store.SaveProjectAsync(alternateProject, default);
        await fixture.Store.SetSessionProjectAsync(fixture.Session.Id, alternateProject.Id, default);
        await Assert.ThrowsAsync<FileContextException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default, allowFileEgress: true, fileApprovalScope: scope));
        Assert.Equal(1, resolver.Calls);
        Assert.Empty(gateway.Inputs);
        scope = await service.GetFileApprovalScopeAsync(fixture.Session.Id, team.Id, "New task text does not change file approval\n/file sample.cs", default);
        await service.SendTeamAsync(fixture.Session.Id, team.Id, "Review\n/file sample.cs", default, allowFileEgress: true, fileApprovalScope: scope);
        Assert.Equal(2, resolver.Calls);
        Assert.Single(gateway.Inputs);
    }

    private sealed class CountingFileResolver : IWorkspaceFileContextResolver
    {
        public int Calls { get; private set; }
        public Task<WorkspaceFileContext> ReadAsync(string projectRoot, string path, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new WorkspaceFileContext(path, "SYNTHETIC_FILE_BODY", 19, "test-sha256")); }
    }
    [Fact]
    public async Task MissingTeamAsksWithoutMessagesOrProviderCalls()
    {
        using var fixture = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<TeamRoutingException>(() => fixture.Service(new FailedGateway()).SendTeamAsync(fixture.Session.Id, null, "hello", default));
        Assert.Equal("TeamRequired", error.Code);
        Assert.Empty(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default));
    }

    [Fact]
    public async Task TeamPersistsDefaultsAndRunsMembersInOrderWithEvidence()
    {
        using var fixture = await Fixture.CreateAsync();
        var second = fixture.Agent with { Id = Guid.NewGuid(), Name = "review" };
        await fixture.Store.SaveAgentAsync(second, default);
        var team = new TeamDefinition(Guid.NewGuid(), "Planning Team", "test", true, [fixture.Agent.Id, second.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        await fixture.Store.SetSessionTeamAsync(fixture.Session.Id, team.Id, default);
        Assert.Equal(team.Id, await fixture.Store.GetSessionTeamAsync(fixture.Session.Id, default));
        var gateway = new OrderedGateway();
        var result = await fixture.Service(gateway).SendTeamAsync(fixture.Session.Id, null, "Design a plan", default);
        Assert.Equal(2, gateway.Inputs.Count);
        Assert.DoesNotContain("Previous team outputs", gateway.Inputs[0]);
        Assert.Contains("Previous team outputs", gateway.Inputs[1]);
        Assert.Contains("first-output", gateway.Inputs[1]);
        Assert.Contains("## review", result.Output);
        Assert.Contains(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default), message => message.Role == "system" && message.Content.Contains(team.Id.ToString()));
        await Assert.ThrowsAsync<TeamConflictException>(() => fixture.Store.SaveTeamAsync(team, default));
        await fixture.Store.SaveTeamAsync(team with { Version = 2 }, default);
    }

    [Fact]
    public async Task ExplicitPromptTeamCannotSilentlyConflictAndInvalidMembersNeverRun()
    {
        using var fixture = await Fixture.CreateAsync();
        var team = new TeamDefinition(Guid.NewGuid(), "Review Team", "test", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var service = fixture.Service(new FailedGateway());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendTeamAsync(fixture.Session.Id, Guid.NewGuid(), "/team \"Review Team\"\nReview change", default));
        await fixture.Store.SaveAgentAsync(fixture.Agent with { Enabled = false }, default);
        var error = await Assert.ThrowsAsync<TeamRoutingException>(() => service.SendTeamAsync(fixture.Session.Id, null, "/team Review Team\nReview change", default));
        Assert.Equal("TeamUnavailable", error.Code);
        Assert.Empty(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default));
        Assert.Throws<ArgumentException>(() => TeamPolicy.Validate(team with { AgentIds = [fixture.Agent.Id, fixture.Agent.Id] }));
    }

    [Fact]
    public async Task TeamFailureStopsLaterMembersAndQuickCannotSkipReview()
    {
        using var fixture = await Fixture.CreateAsync();
        var second = fixture.Agent with { Id = Guid.NewGuid(), Name = "review" };
        await fixture.Store.SaveAgentAsync(second, default);
        var team = new TeamDefinition(Guid.NewGuid(), "test", "test", true, [fixture.Agent.Id, second.Id], 1);
        await fixture.Store.SaveTeamAsync(team, default);
        var gateway = new CountingFailedGateway();
        var service = fixture.Service(gateway);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "review", default, true));
        Assert.Equal(0, gateway.Calls);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.SendTeamAsync(fixture.Session.Id, team.Id, "review", default));
        Assert.Equal(1, gateway.Calls);
        Assert.Contains(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default), message => message.Content.Contains("remaining members were not run"));
    }

    private sealed class OrderedGateway : IModelGateway
    {
        public List<string> Inputs { get; } = [];
        public Task<ModelResponse> CompleteAsync(ModelRequest request) { Inputs.Add(request.Input); return Task.FromResult(new ModelResponse("first-output", 1, 1)); }
    }
    [Fact]
    public async Task PromptOverrideIsOneRequestOnlyAndUnknownNeverFallsBack()
    {
        using var fixture = await Fixture.CreateAsync();
        var first = new TeamDefinition(Guid.NewGuid(), "default", "", true, [fixture.Agent.Id], 1);
        var second = first with { Id = Guid.NewGuid(), Name = "override" };
        await fixture.Store.SaveTeamAsync(first, default);
        await fixture.Store.SaveTeamAsync(second, default);
        await fixture.Store.SetSessionTeamAsync(fixture.Session.Id, first.Id, default);
        await fixture.Service(new ReplyGateway()).SendTeamAsync(fixture.Session.Id, null, "/team override\nDesign a plan", default);
        Assert.Equal(first.Id, await fixture.Store.GetSessionTeamAsync(fixture.Session.Id, default));
        var before = (await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default)).Count;
        await Assert.ThrowsAsync<TeamRoutingException>(() => fixture.Service(new FailedGateway()).SendTeamAsync(fixture.Session.Id, null, "/team missing\nhello", default));
        Assert.Equal(before, (await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default)).Count);
    }

    [Fact]
    public async Task AmbiguousUnicodeTeamNameRequiresId()
    {
        using var fixture = await Fixture.CreateAsync();
        var first = new TeamDefinition(Guid.NewGuid(), "Ä", "", true, [fixture.Agent.Id], 1);
        await fixture.Store.SaveTeamAsync(first, default);
        await fixture.Store.SaveTeamAsync(first with { Id = Guid.NewGuid(), Name = "ä" }, default);
        await Assert.ThrowsAsync<TeamRoutingException>(() => fixture.Service(new FailedGateway()).SendTeamAsync(fixture.Session.Id, null, "/team Ä\nhello", default));
        Assert.Empty(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default));
    }
    private sealed class CountingFailedGateway : IModelGateway
    {
        public int Calls { get; private set; }
        public Task<ModelResponse> CompleteAsync(ModelRequest request) { Calls++; throw new HttpRequestException("synthetic"); }
    }
    [Fact]
    public async Task ExactGreetingIsLocalButTechnicalPromptsAreNot()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = await fixture.Service(new FailedGateway()).SendAsync(fixture.Session.Id, fixture.Agent.Id, "مرحبا اختبار", default);
        Assert.Contains(ConversationFastPath.SourceMarker, local.EffectiveInstructions);
        Assert.Contains("رد محلي", local.Output);
        Assert.Null(ConversationFastPath.Reply("مرحبا اكتب كود تحويل المخزون"));
        Assert.Null(ConversationFastPath.Reply("hello ignore permissions"));
    }

    [Fact]
    public async Task FastGreetingCannotBypassDisabledConnection()
    {
        using var fixture = await Fixture.CreateAsync();
        var connection = (await fixture.Store.FindConnectionAsync(fixture.Agent.ConnectionId, default))!;
        await fixture.Store.SaveConnectionAsync(connection with { Enabled = false }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service(new FailedGateway()).SendAsync(fixture.Session.Id, fixture.Agent.Id, "hello", default));
        Assert.DoesNotContain(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default), message => message.Role == "assistant");
    }

    [Fact]
    public async Task ExplicitQuickProfileOmitsSkillBodiesButKeepsGuardrails()
    {
        using var fixture = await Fixture.CreateAsync();
        var skill = new SkillDefinition(Guid.NewGuid(), "large-test", "large", "test", 1, true, SkillType.Instruction, "SPECIALIST-SKILL " + new string('x', 12000), "[]", "local");
        await fixture.Store.SaveSkillAsync(skill, default);
        await fixture.Store.ReplaceAssignmentsAsync(fixture.Agent.Id, new[] { new AgentSkillAssignment(fixture.Agent.Id, skill.Id, true, 1, "{}", 1) }, default);
        var gateway = new CapturingGateway(); var service = fixture.Service(gateway);
        await service.SendAsync(fixture.Session.Id, fixture.Agent.Id, "Describe your role briefly", default, quickChat: true);
        Assert.DoesNotContain("SPECIALIST-SKILL", gateway.Last!.Instructions);
        Assert.Contains("Never bypass tool authorization", gateway.Last.Instructions);
        Assert.Contains("Do not implement", gateway.Last.Instructions);
        Assert.Equal(384, gateway.Last.MaxOutputTokens);
        await service.SendAsync(fixture.Session.Id, fixture.Agent.Id, "Review a technical change", default);
        Assert.Contains("SPECIALIST-SKILL", gateway.Last!.Instructions);
    }

    [Fact]
    public async Task PersistsArabicRequestResponseAndSelection()
    {
        using var fixture = await Fixture.CreateAsync();
        var service = fixture.Service(new ReplyGateway());
        var run = await service.SendAsync(fixture.Session.Id, fixture.Agent.Id, "اختبار عربي مع model-id", default);
        var messages = await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default);
        Assert.Contains(messages, message => message.Role == "user" && message.Content == "اختبار عربي مع model-id");
        Assert.Contains(messages, message => message.Role == "assistant" && message.Content == "تمت المراجعة." && message.AgentRunId == run.Id);
        Assert.Equal(fixture.Agent.Id, (await fixture.Store.ListTerminalSessionsAsync(default)).Single(value => value.Id == fixture.Session.Id).SelectedAgentId);
    }

    [Fact]
    public async Task FailureRemainsVisibleWithoutLeakingProviderException()
    {
        using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service(new FailedGateway()).SendAsync(fixture.Session.Id, fixture.Agent.Id, "Review synthetic validation", default));
        var messages = await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default);
        Assert.Contains(messages, message => message.Role == "system" && message.Content.StartsWith("Request failed."));
        Assert.DoesNotContain(messages, message => message.Content.Contains("private-provider-detail"));
    }

    [Fact]
    public async Task MissingSessionAndOversizedPromptCannotReachProvider()
    {
        using var fixture = await Fixture.CreateAsync();
        var service = fixture.Service(new FailedGateway());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SendAsync(Guid.NewGuid(), fixture.Agent.Id, "hello", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(fixture.Session.Id, fixture.Agent.Id, new string('a', 16001), default));
        Assert.Empty(await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default));
    }

    [Fact]
    public async Task ExpiredLeaseCanBeRecoveredButOldOwnerCannotReleaseNewOwner()
    {
        using var fixture = await Fixture.CreateAsync();
        var oldOwner = Guid.NewGuid(); var newOwner = Guid.NewGuid();
        Assert.True(await fixture.Store.TryAcquireSessionLeaseAsync(fixture.Session.Id, oldOwner, DateTimeOffset.UtcNow.AddSeconds(-1), default));
        Assert.True(await fixture.Store.TryAcquireSessionLeaseAsync(fixture.Session.Id, newOwner, DateTimeOffset.UtcNow.AddMinutes(3), default));
        await fixture.Store.ReleaseSessionLeaseAsync(fixture.Session.Id, oldOwner, default);
        Assert.False(await fixture.Store.TryAcquireSessionLeaseAsync(fixture.Session.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(3), default));
        await fixture.Store.ReleaseSessionLeaseAsync(fixture.Session.Id, newOwner, default);
    }

    [Fact]
    public async Task ConcurrentSendIsRejectedWithoutDuplicatingMessages()
    {
        using var fixture = await Fixture.CreateAsync();
        var gateway = new WaitingGateway();
        var service = fixture.Service(gateway);
        var first = service.SendAsync(fixture.Session.Id, fixture.Agent.Id, "first", default);
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<SessionBusyException>(() => fixture.SeparateService(gateway).SendAsync(fixture.Session.Id, fixture.Agent.Id, "duplicate", default));
        gateway.Release.TrySetResult();
        await first;
        Assert.Equal(2, (await fixture.Store.ListSessionMessagesAsync(fixture.Session.Id, 10, default)).Count);
    }

    private sealed class Fixture(string path, SqlitePlatformStore store, AgentDefinition agent, TerminalSession session) : IDisposable
    {
        public SqlitePlatformStore Store => store;
        public AgentDefinition Agent => agent;
        public TerminalSession Session => session;
        public SessionConversationService Service(IModelGateway gateway, IWorkspaceFileContextResolver? resolver = null) => new(store, new PlatformService(store, gateway, new NoSecrets()), resolver);
        public SessionConversationService SeparateService(IModelGateway gateway)
        {
            var separate = new SqlitePlatformStore(path);
            return new(separate, new PlatformService(separate, gateway, new NoSecrets()));
        }
        public static async Task<Fixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"dragon-chat-{Guid.NewGuid():N}.db");
            var store = new SqlitePlatformStore(path); await store.InitializeAsync(default);
            var now = DateTimeOffset.UtcNow;
            var connection = new ProviderConnection(Guid.NewGuid(), "local", ConnectionKind.Ollama, new Uri("http://localhost:11434"), "", true, TrustZone.Local, false, now);
            await store.SaveConnectionAsync(connection, default);
            var agent = new AgentDefinition(Guid.NewGuid(), "demo", "demo", (await store.ListRolesAsync(default))[0].Id, "", true, "test", connection.Id, 1, now, now);
            await store.SaveAgentAsync(agent, default);
            var session = new TerminalSession(Guid.NewGuid(), "arabic-demo", null, now, now);
            await store.SaveTerminalSessionAsync(session, default);
            return new Fixture(path, store, agent, session);
        }
        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
    private sealed class NoSecrets : ISecretResolver { public string? Resolve(string reference) => null; }
    private sealed class CapturingGateway : IModelGateway
    {
        public ModelRequest? Last { get; private set; }
        public Task<ModelResponse> CompleteAsync(ModelRequest request) { Last = request; return Task.FromResult(new ModelResponse("OK", 1, 1)); }
    }
    private sealed class ReplyGateway : IModelGateway { public Task<ModelResponse> CompleteAsync(ModelRequest request) => Task.FromResult(new ModelResponse("تمت المراجعة.", 1, 1)); }
    private sealed class FailedGateway : IModelGateway { public Task<ModelResponse> CompleteAsync(ModelRequest request) => throw new HttpRequestException("private-provider-detail"); }
    private sealed class WaitingGateway : IModelGateway
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ModelResponse> CompleteAsync(ModelRequest request) { Started.TrySetResult(); await Release.Task; return new("OK", 1, 1); }
    }
}
