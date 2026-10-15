using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class DefaultCouncilTests
{
    [Fact]
    public async Task ProvisioningCreatesThreeAgentsWithEditableDefaultSkillAssignmentsAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-{Guid.NewGuid():N}.db");
        var store = new SqlitePlatformStore(path);
        var connectionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        try
        {
            await store.InitializeAsync(CancellationToken.None);
            await store.SaveConnectionAsync(new ProviderConnection(connectionId, "council-connection", ConnectionKind.OpenAiCompatible, new Uri("https://provider.example/"), "env:TEST_SECRET", true, TrustZone.Local, false, now), CancellationToken.None);
            var service = new PlatformService(store, new UnusedGateway(), new UnusedSecretResolver());

            var provisioned = await service.ProvisionDefaultCouncilAsync(new DefaultCouncilProvisionRequest(connectionId, "test-model"), CancellationToken.None);

            Assert.Equal(3, provisioned.Count);
            Assert.Contains(provisioned, agent => agent.Name == "Execution Agent");
            Assert.Contains(provisioned, agent => agent.Name == "Quality Review Agent");
            Assert.Contains(provisioned, agent => agent.Name == "Security Test Agent");
            foreach (var agent in provisioned) Assert.Equal(4, (await store.ListAssignmentsAsync(agent.Id, CancellationToken.None)).Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }

    private sealed class UnusedGateway : IModelGateway
    {
        public Task<ModelResponse> CompleteAsync(ModelRequest request) => throw new NotSupportedException();
    }

    private sealed class UnusedSecretResolver : ISecretResolver
    {
        public string? Resolve(string secretReference) => null;
    }
}
