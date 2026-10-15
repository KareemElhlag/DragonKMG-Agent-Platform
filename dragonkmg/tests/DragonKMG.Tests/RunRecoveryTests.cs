using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class RunRecoveryTests
{
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
            await store.SaveConnectionAsync(new ProviderConnection(connectionId, "test-connection", ConnectionKind.OpenAiCompatible, new Uri("https://provider.example/"), "env:TEST_SECRET", true, TrustZone.Local, false, now), CancellationToken.None);
            await store.SaveAgentAsync(new AgentDefinition(agentId, "test-agent", "test", role.Id, "instructions", true, "test-model", connectionId, 1, now, now), CancellationToken.None);
            await store.SaveRunAsync(running, CancellationToken.None);
            await store.SaveRunAsync(completed, CancellationToken.None);

            var recovered = await store.RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var runs = await store.ListRunsAsync(agentId, CancellationToken.None);

            Assert.Equal(1, recovered);
            Assert.Contains(runs, run => run.Id == running.Id && run.Status == RunStatus.Failed && run.ErrorCode == "InterruptedByRestart" && run.FinishedAt is not null);
            Assert.Contains(runs, run => run.Id == completed.Id && run.Status == RunStatus.Succeeded && run.Output == "done");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }
}
