using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class SessionConversationTests
{
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
        public SessionConversationService Service(IModelGateway gateway) => new(store, new PlatformService(store, gateway, new NoSecrets()));
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
