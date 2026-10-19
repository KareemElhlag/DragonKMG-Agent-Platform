using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DragonKMG.Tests;

public sealed class FeatureTeamProvisionTests
{
    [Fact]
    public async Task TemplateExposesOrderedEightRoleProductionLine()
    {
        using var fixture = await Fixture.CreateAsync();
        var template = Assert.Single(await fixture.Service.GetFeatureTeamTemplatesAsync(default));

        Assert.Equal("nexus-feature-full-production", template.Key);
        Assert.Equal("Nexus Feature Full Production", template.Name);
        Assert.Equal(["Nexus Feature Planner", "Nexus Feature Implementer", "Nexus Feature Test & Compatibility", "Nexus Feature Platform UX", "Nexus Feature Performance", "Nexus Feature Master Auditor", "Nexus Feature Security Reviewer", "Nexus Feature Release Certifier"], template.Agents.Select(agent => agent.Name));
        Assert.Equal(AgentMode.PlanOnly, template.Agents[0].Mode);
        Assert.Equal(AgentMode.Execution, template.Agents[1].Mode);
        Assert.All(template.Agents.Skip(2), agent => Assert.Equal(AgentMode.Review, agent.Mode));
        Assert.Contains("nexusecore-ui-ux", template.Agents[3].SkillKeys);
        Assert.Contains("flutter-architect-review", template.Agents[3].SkillKeys);
        Assert.Contains("no conditional skip", template.Agents[3].Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planning 10", template.Agents[7].Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nexus-feature-performance/SKILL.md", FeatureTeamSkills.Additional[0].SourceRef);
        Assert.Equal("nexus-master-audit-skill", FeatureTeamSkills.Additional[1].Key);
        Assert.Contains("nexus-master-audit-skill/SKILL.md", FeatureTeamSkills.Additional[1].SourceRef);
    }

    [Fact]
    public async Task ProvisioningAssignsOnlyTemplateSkillsAndIsIdempotent()
    {
        using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request;
        var first = await fixture.Service.ProvisionFeatureTeamAsync(request, default);
        var second = await fixture.Service.ProvisionFeatureTeamAsync(request, default);

        Assert.Equal(NexusFeatureTeam.Name, first.Team.Name);
        Assert.Equal(1, first.Team.Version);
        Assert.Equal(NexusFeatureTeam.Agents.Select(agent => agent.Name), first.Agents.Select(agent => agent.Name));
        Assert.Equal(first.Team.Id, second.Team.Id);
        Assert.Equal(first.Agents.Select(agent => agent.Id), second.Agents.Select(agent => agent.Id));
        Assert.Equal(8, (await fixture.Store.ListAgentsAsync(default)).Count);
        Assert.Empty(await fixture.Store.ListToolGrantsAsync(first.Agents[0].Id, default));

        var skillsById = (await fixture.Store.ListSkillsAsync(default)).ToDictionary(skill => skill.Id, skill => skill.Key);
        foreach (var (agent, template) in first.Agents.Zip(NexusFeatureTeam.Agents))
        {
            Assert.Equal(template.RoleId, agent.RoleId);
            Assert.Equal(template.Mode, agent.Mode);
            Assert.Equal(fixture.Request.ModelId, agent.ModelId);
            var assignments = await fixture.Store.ListAssignmentsAsync(agent.Id, default);
            Assert.Equal(template.SkillKeys.Order(StringComparer.Ordinal), assignments.Select(assignment => skillsById[assignment.SkillId]).Order(StringComparer.Ordinal));
            Assert.All(assignments, assignment => Assert.True(assignment.Enabled));
        }
    }

    [Fact]
    public async Task MissingOrDisabledModelFailsClosedBeforeCreatingAgents()
    {
        using var fixture = await Fixture.CreateAsync(registerModel: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ProvisionFeatureTeamAsync(fixture.Request, default));
        Assert.Empty(await fixture.Store.ListAgentsAsync(default));
        Assert.Empty(await fixture.Store.ListTeamsAsync(default));
    }

    [Fact]
    public async Task ExistingCustomAgentAssignmentsAndConfigurationArePreserved()
    {
        using var fixture = await Fixture.CreateAsync();
        var plannerTemplate = NexusFeatureTeam.Agents[0];
        var customSkill = new SkillDefinition(Guid.NewGuid(), "planner-custom", "Planner Custom", "Custom binding", 1, true, SkillType.Instruction, "custom", "[]", "user");
        await fixture.Store.SaveSkillAsync(customSkill, default);
        var now = DateTimeOffset.UtcNow;
        var planner = new AgentDefinition(Guid.NewGuid(), plannerTemplate.Name, "Edited planner description", plannerTemplate.RoleId, "User-edited planner prompt", true, "custom-model", fixture.Request.ConnectionId, 2, now, now, AgentMode.PlanOnly);
        await fixture.Store.SaveAgentAsync(planner, default);
        var customAssignment = new AgentSkillAssignment(planner.Id, customSkill.Id, true, 1, "{\"custom\":true}", 3);
        await fixture.Store.ReplaceAssignmentsAsync(planner.Id, [customAssignment], default);

        var result = await fixture.Service.ProvisionFeatureTeamAsync(fixture.Request, default);
        var provisionedPlanner = result.Agents[0];
        var assignments = await fixture.Store.ListAssignmentsAsync(planner.Id, default);

        Assert.Equal("User-edited planner prompt", provisionedPlanner.Instructions);
        Assert.Equal("custom-model", provisionedPlanner.ModelId);
        Assert.Contains(assignments, assignment => assignment == customAssignment);
        var skillsById = (await fixture.Store.ListSkillsAsync(default)).ToDictionary(skill => skill.Id, skill => skill.Key);
        var assignedKeys = assignments.Select(assignment => skillsById[assignment.SkillId]).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("planner-custom", assignedKeys);
        Assert.Contains("kmg-engineering-standard", assignedKeys);
    }

    private sealed class Fixture(string path, SqlitePlatformStore store, PlatformService service, FeatureTeamProvisionRequest request) : IDisposable
    {
        public SqlitePlatformStore Store { get; } = store;
        public PlatformService Service { get; } = service;
        public FeatureTeamProvisionRequest Request { get; } = request;

        public static async Task<Fixture> CreateAsync(bool registerModel = true)
        {
            var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-team-{Guid.NewGuid():N}.db");
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(default);
            var connectionId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            await store.SaveConnectionAsync(new ProviderConnection(connectionId, "feature-team-provider", ConnectionKind.OpenAiCompatible, new Uri("https://provider.example/"), "env:FEATURE_TEAM_TEST", true, TrustZone.RemoteSelfHosted, false, now), default);
            const string model = "provider/test-model";
            if (registerModel)
                await store.SaveConnectionModelAsync(new ConnectionModel(connectionId, model, "Test Model", true, true, "test", now), false, default);
            var service = new PlatformService(store, new UnusedGateway(), new TestSecretResolver());
            return new Fixture(path, store, service, new FeatureTeamProvisionRequest(NexusFeatureTeam.Key, connectionId, model));
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }

    private sealed class UnusedGateway : IModelGateway
    {
        public Task<ModelResponse> CompleteAsync(ModelRequest request) => throw new NotSupportedException("Provisioning must not call a model gateway.");
    }

    private sealed class TestSecretResolver : ISecretResolver
    {
        public string? Resolve(string secretReference) => null;
    }
}
