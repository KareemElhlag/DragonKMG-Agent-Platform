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
            var gateway = new CapturingGateway();
            var service = new PlatformService(store, gateway, new TestSecretResolver());

            var provisioned = await service.ProvisionDefaultCouncilAsync(new DefaultCouncilProvisionRequest(connectionId, "test-model"), CancellationToken.None);

            Assert.Equal(3, provisioned.Count);
            Assert.Contains(provisioned, agent => agent.Name == "Execution Agent");
            Assert.Contains(provisioned, agent => agent.Name == "Quality Review Agent");
            Assert.Contains(provisioned, agent => agent.Name == "Security Test Agent");
            foreach (var agent in provisioned) Assert.Equal(4, (await store.ListAssignmentsAsync(agent.Id, CancellationToken.None)).Count);

            var councilRun = await service.RunDefaultCouncilAsync("Implement inventory validation", CancellationToken.None);

            Assert.NotEqual(Guid.Empty, councilRun.SecurityReviewRunId);
            Assert.Equal(3, gateway.Requests.Count);
            Assert.Contains("test-model result", gateway.Requests[1].Input);
            Assert.Equal(2, gateway.Requests[2].Input.Split("test-model result", StringSplitOptions.None).Length - 1);

            var executionAgent = provisioned.Single(agent => agent.Name == "Execution Agent");
            var toolSkill = new SkillDefinition(Guid.NewGuid(), "repo-search", "Repository Search", "Read-only repository search.", 1, true, SkillType.ToolBacked, "Use repository search only when granted.", "[\"repo.search\"]", "local", "user", "");
            await store.SaveSkillAsync(toolSkill, CancellationToken.None);
            var assignments = (await store.ListAssignmentsAsync(executionAgent.Id, CancellationToken.None)).Append(new AgentSkillAssignment(executionAgent.Id, toolSkill.Id, true, 99, "{}", 1)).ToArray();
            await store.ReplaceAssignmentsAsync(executionAgent.Id, assignments, CancellationToken.None);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEffectiveConfigurationAsync(executionAgent.Id, CancellationToken.None));
            await store.ReplaceToolGrantsAsync(executionAgent.Id, [new AgentToolGrant(executionAgent.Id, "repo.search", true, 1)], CancellationToken.None);
            var configuration = await service.GetEffectiveConfigurationAsync(executionAgent.Id, CancellationToken.None);
            Assert.Contains(configuration.ToolGrants, grant => grant.ToolId == "repo.search");

            var planAgent = new AgentDefinition(Guid.NewGuid(), "plan-agent", "planning", executionAgent.RoleId, "Help plan safely.", true, "test-model", connectionId, 1, now, now, AgentMode.PlanOnly);
            await store.SaveAgentAsync(planAgent, CancellationToken.None);
            var planConfiguration = await service.GetEffectiveConfigurationAsync(planAgent.Id, CancellationToken.None);
            Assert.Contains("plan only", planConfiguration.Instructions, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not provide source code", planConfiguration.Instructions, StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }

    private sealed class CapturingGateway : IModelGateway
    {
        public List<ModelRequest> Requests { get; } = [];
        public Task<ModelResponse> CompleteAsync(ModelRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(new ModelResponse($"{request.ModelId} result", null, null));
        }
    }

    private sealed class TestSecretResolver : ISecretResolver
    {
        public string? Resolve(string secretReference) => "test-secret";
    }
}
