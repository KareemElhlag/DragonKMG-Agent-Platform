using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class DefaultConnectionTests
{
    [Fact]
    public async Task DefaultPersistsWithoutChangingExistingAgentBindings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-{Guid.NewGuid():N}.db");
        var ct = CancellationToken.None;
        try
        {
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(ct);
            Assert.Null(await store.GetDefaultConnectionAsync(ct));
            var now = DateTimeOffset.UtcNow;
            var first = new ProviderConnection(Guid.NewGuid(), "local", ConnectionKind.Ollama, new Uri("http://127.0.0.1:11434"), "env:LOCAL", true, TrustZone.Local, false, now);
            var second = first with { Id = Guid.NewGuid(), Name = "remote", Endpoint = new Uri("https://remote.example"), TrustZone = TrustZone.RemoteSelfHosted };
            await store.SaveConnectionAsync(first, ct);
            await store.SaveConnectionAsync(second, ct);
            var role = (await store.ListRolesAsync(ct))[0];
            var agent = new AgentDefinition(Guid.NewGuid(), "isolated", "test", role.Id, "", true, "test-model", first.Id, 1, now, now);
            await store.SaveAgentAsync(agent, ct);
            await store.SetDefaultConnectionAsync(second.Id, ct);

            var reopened = new SqlitePlatformStore(path);
            Assert.Equal(second.Id, await reopened.GetDefaultConnectionAsync(ct));
            Assert.Equal(first.Id, (await reopened.FindAgentAsync(agent.Id, ct))!.ConnectionId);
            await Assert.ThrowsAsync<ArgumentException>(() => store.SetDefaultConnectionAsync(Guid.NewGuid(), ct));
            await store.SaveConnectionAsync(second with { Enabled = false }, ct);
            await Assert.ThrowsAsync<ArgumentException>(() => store.SetDefaultConnectionAsync(second.Id, ct));
            await store.SetDefaultConnectionAsync(null, ct);
            Assert.Null(await reopened.GetDefaultConnectionAsync(ct));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
