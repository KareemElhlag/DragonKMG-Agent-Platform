using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class PlatformService(IPlatformStore store, IModelGateway gateway, ISecretResolver secrets)
{
    public async Task<EffectiveAgentConfiguration> GetEffectiveConfigurationAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await store.FindAgentAsync(agentId, cancellationToken) ?? throw new KeyNotFoundException("Agent was not found.");
        var role = await store.FindRoleAsync(agent.RoleId, cancellationToken) ?? throw new InvalidOperationException("Agent role was not found.");
        var assignments = (await store.ListAssignmentsAsync(agentId, cancellationToken)).Where(x => x.Enabled).OrderBy(x => x.Priority).ToArray();
        var skills = new List<SkillDefinition>();
        foreach (var assignment in assignments)
        {
            var skill = await store.FindSkillAsync(assignment.SkillId, cancellationToken);
            if (skill is { Enabled: true }) skills.Add(skill);
        }

        var instructionParts = new[] { "You are operating inside DragonKMG. Never bypass tool authorization.", role.DefaultInstructions, agent.Instructions }
            .Concat(skills.Where(x => x.Type == SkillType.Instruction).Select(x => x.InstructionContent))
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return new EffectiveAgentConfiguration(agent, role, skills, assignments, string.Join("\n\n", instructionParts));
    }

    public async Task<AgentRun> RunAgentAsync(Guid agentId, string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input is required.", nameof(input));
        var config = await GetEffectiveConfigurationAsync(agentId, cancellationToken);
        if (!config.Agent.Enabled) throw new InvalidOperationException("Agent is disabled.");
        var connection = await store.FindConnectionAsync(config.Agent.ConnectionId, cancellationToken) ?? throw new InvalidOperationException("Model connection was not found.");
        if (!connection.Enabled) throw new InvalidOperationException("Model connection is disabled.");
        if (connection.TrustZone == TrustZone.Cloud && !connection.CloudEgressAllowed) throw new UnauthorizedAccessException("Cloud egress is not authorized for this connection.");
        var secret = secrets.Resolve(connection.SecretReference) ?? throw new InvalidOperationException("The configured secret reference is unavailable.");
        var started = DateTimeOffset.UtcNow;
        var run = new AgentRun(Guid.NewGuid(), config.Agent.Id, config.Agent.Version, input, config.Instructions, RunStatus.Running, null, null, started, null);
        await store.SaveRunAsync(run, cancellationToken);
        try
        {
            var response = await gateway.CompleteAsync(new ModelRequest(connection.Endpoint, secret, config.Agent.ModelId, config.Instructions, input, cancellationToken));
            var completed = run with { Status = RunStatus.Succeeded, Output = response.Text, FinishedAt = DateTimeOffset.UtcNow };
            await store.SaveRunAsync(completed, cancellationToken);
            return completed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = run with { Status = RunStatus.Failed, ErrorCode = exception.GetType().Name, FinishedAt = DateTimeOffset.UtcNow };
            await store.SaveRunAsync(failed, cancellationToken);
            throw;
        }
    }
}
