using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class PlatformService(IPlatformStore store, IModelGateway gateway, ISecretResolver secrets)
{
    public async Task<IReadOnlyList<AgentDefinition>> ProvisionDefaultCouncilAsync(DefaultCouncilProvisionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId)) throw new ArgumentException("A model ID is required.", nameof(request));
        var connection = await store.FindConnectionAsync(request.ConnectionId, cancellationToken) ?? throw new KeyNotFoundException("Model connection was not found.");
        if (!connection.Enabled) throw new InvalidOperationException("Model connection is disabled.");
        var existing = await store.ListAgentsAsync(cancellationToken);
        var defaults = (await store.ListSkillsAsync(cancellationToken)).Where(skill => skill.Enabled && skill.SourceType == "local-codex-skill").OrderBy(skill => skill.Name).ToArray();
        var result = new List<AgentDefinition>();
        foreach (var template in DefaultCouncil.Agents)
        {
            var agent = existing.FirstOrDefault(value => value.Name == template.Name);
            if (agent is null)
            {
                var now = DateTimeOffset.UtcNow;
                agent = new AgentDefinition(Guid.NewGuid(), template.Name, template.Description, template.RoleId, template.Instructions, true, request.ModelId.Trim(), request.ConnectionId, 1, now, now);
                await store.SaveAgentAsync(agent, cancellationToken);
                var assignments = defaults.Select((skill, index) => new AgentSkillAssignment(agent.Id, skill.Id, true, index + 1, "{}", 1)).ToArray();
                await store.ReplaceAssignmentsAsync(agent.Id, assignments, cancellationToken);
            }
            result.Add(agent);
        }
        return result;
    }

    public async Task<CouncilRun> RunDefaultCouncilAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input is required.", nameof(input));
        var agents = await store.ListAgentsAsync(cancellationToken);
        var execution = FindCouncilAgent(agents, "Execution Agent");
        var quality = FindCouncilAgent(agents, "Quality Review Agent");
        var security = FindCouncilAgent(agents, "Security Test Agent");

        var executionRun = await RunAgentAsync(execution.Id, BuildWorkflowInput(input, DefaultCouncil.Workflow[0], DefaultCouncil.Workflow[1]), cancellationToken);
        var qualityRun = await RunAgentAsync(quality.Id, BuildReviewInput(input, [DefaultCouncil.Workflow[2]], executionRun.Output), cancellationToken);
        var securityRun = await RunAgentAsync(security.Id, BuildReviewInput(input, [DefaultCouncil.Workflow[3]], executionRun.Output, qualityRun.Output), cancellationToken);
        return new CouncilRun(executionRun.Id, qualityRun.Id, securityRun.Id);
    }

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

    private static AgentDefinition FindCouncilAgent(IReadOnlyList<AgentDefinition> agents, string name) => agents.FirstOrDefault(agent => agent.Name == name && agent.Enabled) ?? throw new InvalidOperationException($"Default council agent '{name}' is missing or disabled. Provision the council before running it.");

    private static string BuildWorkflowInput(string request, params CouncilWorkflowStep[] steps) => $"Original request:\n{request}\n\nRequired workflow steps:\n{FormatSteps(steps)}\n\nDo not claim a check passed without evidence. Return the expected evidence and explicitly name unknowns or blockers.";

    private static string BuildReviewInput(string request, IReadOnlyList<CouncilWorkflowStep> steps, params string?[] evidence)
    {
        const int maximumEvidenceCharacters = 12000;
        var material = string.Join("\n\n---\n\n", evidence.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Length <= maximumEvidenceCharacters ? value : value[..maximumEvidenceCharacters]));
        return $"Original request:\n{request}\n\nRequired workflow steps:\n{FormatSteps(steps)}\n\nEvidence from prior stage(s):\n{material}\n\nDo not invent evidence. Mark every unchecked item and every limitation explicitly.";
    }

    private static string FormatSteps(IEnumerable<CouncilWorkflowStep> steps) => string.Join("\n", steps.Select(step => $"{step.Name} ({step.Owner})\nChecklist:\n{string.Join("\n", step.Checklist.Select(item => $"- [ ] {item}"))}\nExpected evidence: {step.ExpectedEvidence}"));
}
