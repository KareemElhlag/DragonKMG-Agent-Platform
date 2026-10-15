using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.Application;

namespace DragonKMG.Tests;

public sealed class RunRecoveryTests
{
    [Fact]
    public async Task CancelledProviderRunDoesNotRemainRunning()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragon-cancel-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(default);
            var now = DateTimeOffset.UtcNow;
            var connection = new ProviderConnection(Guid.NewGuid(), "local", ConnectionKind.Ollama, new Uri("http://localhost:11434"), "", true, TrustZone.Local, false, now);
            await store.SaveConnectionAsync(connection, default);
            var role = (await store.ListRolesAsync(default))[0];
            var agent = new AgentDefinition(Guid.NewGuid(), "cancel-test", "test", role.Id, "test", true, "test", connection.Id, 1, now, now);
            await store.SaveAgentAsync(agent, default);
            var service = new PlatformService(store, new CancelledGateway(), new EmptySecrets());
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAgentAsync(agent.Id, "test", default));
            var run = Assert.Single(await store.ListRunsAsync(agent.Id, default));
            Assert.Equal(RunStatus.Failed, run.Status);
            Assert.Equal("OperationCanceled", run.ErrorCode);
            Assert.NotNull(run.FinishedAt);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
    private sealed class CancelledGateway : IModelGateway
    {
        public Task<ModelResponse> CompleteAsync(ModelRequest request) => throw new OperationCanceledException();
    }
    private sealed class EmptySecrets : ISecretResolver { public string? Resolve(string reference) => null; }

    [Fact]
    public async Task RecoverInterruptedRunsMarksOnlyIncompleteRunningRunsAsFailedAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-{Guid.NewGuid():N}.db");
        var store = new SqlitePlatformStore(path);
        var agentId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var running = new AgentRun(Guid.NewGuid(), agentId, 1, "input", "instructions", RunStatus.Running, null, null, DateTimeOffset.UtcNow, null);
        var completed = new AgentRun(Guid.NewGuid(), agentId, 1, "input", "instructions", RunStatus.Succeeded, "done", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        try
        {
            await store.InitializeAsync(CancellationToken.None);
            var role = (await store.ListRolesAsync(CancellationToken.None)).Single(value => value.Name == "Execution Engineer");
            await store.SaveConnectionAsync(new ProviderConnection(connectionId, "test-connection", ConnectionKind.OpenAiCompatible, new Uri("https://provider.example/"), "env:TEST_SECRET", true, TrustZone.RemoteSelfHosted, false, now), CancellationToken.None);
            await store.SaveAgentAsync(new AgentDefinition(agentId, "test-agent", "test", role.Id, "instructions", true, "test-model", connectionId, 1, now, now), CancellationToken.None);
            await store.SaveRunAsync(running, CancellationToken.None);
            await store.SaveRunAsync(completed, CancellationToken.None);

            var recovered = await store.RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var runs = await store.ListRunsAsync(agentId, CancellationToken.None);

            Assert.Equal(1, recovered);
            Assert.Contains(runs, run => run.Id == running.Id && run.Status == RunStatus.Failed && run.ErrorCode == "InterruptedByRestart" && run.FinishedAt is not null);
            Assert.Contains(runs, run => run.Id == completed.Id && run.Status == RunStatus.Succeeded && run.Output == "done");
            await store.SaveAuditEventAsync(new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "test-actor", "POST /api/test", 201, "succeeded"), CancellationToken.None);
            var auditEvents = await store.ListAuditEventsAsync(10, CancellationToken.None);
            Assert.Contains(auditEvents, auditEvent => auditEvent.Actor == "test-actor" && auditEvent.StatusCode == 201);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }
}
