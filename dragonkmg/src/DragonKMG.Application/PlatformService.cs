using DragonKMG.Domain;
using System.Text.Json;

namespace DragonKMG.Application;

public sealed class PlatformService(IPlatformStore store, IModelGateway gateway, ISecretResolver secrets, ISkillContentResolver? skillContents = null)
{
    public Task<IReadOnlyList<FeatureTeamTemplate>> GetFeatureTeamTemplatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FeatureTeamTemplate>>(NexusFeatureTeam.Templates);
    }

    public async Task<ProvisionedFeatureTeam> ProvisionFeatureTeamAsync(FeatureTeamProvisionRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.TemplateKey, NexusFeatureTeam.Key, StringComparison.Ordinal))
            throw new KeyNotFoundException("Feature team template was not found.");
        if (request.ConnectionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ModelId) || request.ModelId.Length > 256 || request.ModelId.Any(char.IsControl))
            throw new ArgumentException("A valid model connection and model ID are required.", nameof(request));

        var connection = await store.FindConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Model connection was not found.");
        if (!connection.Enabled) throw new InvalidOperationException("Model connection is disabled.");
        var canonicalModelId = ModelIdentifier.Canonical(connection.Kind, request.ModelId);
        var registeredModel = (await store.ListConnectionModelsAsync(connection.Id, cancellationToken))
            .FirstOrDefault(model => model.Enabled && ModelIdentifier.Canonical(connection.Kind, model.ModelId) == canonicalModelId);
        if (registeredModel is null) throw new InvalidOperationException("The selected model is not registered and enabled for this connection.");

        var existingTeams = await store.ListTeamsAsync(cancellationToken);
        var existingTeam = existingTeams.FirstOrDefault(team => string.Equals(team.Name, NexusFeatureTeam.Name, StringComparison.OrdinalIgnoreCase));
        if (existingTeam is not null)
        {
            TeamPolicy.Validate(existingTeam);
            var persistedAgents = new List<AgentDefinition>(existingTeam.AgentIds.Count);
            foreach (var agentId in existingTeam.AgentIds)
                persistedAgents.Add(await store.FindAgentAsync(agentId, cancellationToken) ?? throw new FeatureTeamProvisionConflictException("The existing feature team references a missing agent."));
            if (persistedAgents.Count != NexusFeatureTeam.Agents.Count || !persistedAgents.Select(agent => agent.Name).SequenceEqual(NexusFeatureTeam.Agents.Select(agent => agent.Name), StringComparer.Ordinal))
                throw new FeatureTeamProvisionConflictException("The existing feature team does not match the provisioned template order.");
            return new ProvisionedFeatureTeam(existingTeam, persistedAgents);
        }

        var allSkills = await store.ListSkillsAsync(cancellationToken);
        var skillByKey = allSkills.Where(skill => skill.Enabled).ToDictionary(skill => skill.Key, StringComparer.OrdinalIgnoreCase);
        var requiredSkillKeys = NexusFeatureTeam.Agents.SelectMany(agent => agent.SkillKeys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var missingSkills = requiredSkillKeys.Where(key => !skillByKey.ContainsKey(key)).ToArray();
        if (missingSkills.Length > 0) throw new InvalidOperationException($"Required feature team skills are missing or disabled: {string.Join(", ", missingSkills)}.");

        var allRoles = await store.ListRolesAsync(cancellationToken);
        var roleIds = allRoles.Where(role => role.IsSystem).Select(role => role.Id).ToHashSet();
        var missingRoles = NexusFeatureTeam.Agents.Where(agent => !roleIds.Contains(agent.RoleId)).Select(agent => agent.RoleId).Distinct().ToArray();
        if (missingRoles.Length > 0) throw new InvalidOperationException("Required feature team roles are not registered.");

        var existingAgents = await store.ListAgentsAsync(cancellationToken);
        // Detect deterministic name/role collisions before the first write, so a bad template
        // collision cannot leave a partially provisioned team behind.
        foreach (var template in NexusFeatureTeam.Agents)
        {
            var collision = existingAgents.FirstOrDefault(value => string.Equals(value.Name, template.Name, StringComparison.OrdinalIgnoreCase));
            if (collision is not null && (collision.RoleId != template.RoleId || collision.Mode != template.Mode))
                throw new FeatureTeamProvisionConflictException($"An existing custom agent named '{template.Name}' has an incompatible role or mode.");
        }
        var agents = new List<AgentDefinition>(NexusFeatureTeam.Agents.Count);
        var now = DateTimeOffset.UtcNow;
        foreach (var template in NexusFeatureTeam.Agents)
        {
            var agent = existingAgents.FirstOrDefault(value => string.Equals(value.Name, template.Name, StringComparison.OrdinalIgnoreCase));
            if (agent is null)
            {
                agent = new AgentDefinition(Guid.NewGuid(), template.Name, template.Description, template.RoleId, template.Instructions, true,
                    canonicalModelId, connection.Id, 1, now, now, template.Mode);
                await store.SaveAgentAsync(agent, cancellationToken);
                var assignments = template.SkillKeys.Select((key, index) =>
                    new AgentSkillAssignment(agent.Id, skillByKey[key].Id, true, index + 1, "{}", 1)).ToArray();
                await store.ReplaceAssignmentsAsync(agent.Id, assignments, cancellationToken);
            }
            else
            {
                // Recover a partially completed prior provisioning without replacing edited instructions,
                // connection/model, enabled state, tool grants, or any existing skill assignments.
                var currentAssignments = await store.ListAssignmentsAsync(agent.Id, cancellationToken);
                var existingSkillIds = currentAssignments.Select(assignment => assignment.SkillId).ToHashSet();
                var nextPriority = currentAssignments.Select(assignment => assignment.Priority).DefaultIfEmpty(0).Max() + 1;
                var additions = template.SkillKeys.Select(key => skillByKey[key]).Where(skill => !existingSkillIds.Contains(skill.Id))
                    .Select((skill, index) => new AgentSkillAssignment(agent.Id, skill.Id, true, nextPriority + index, "{}", 1)).ToArray();
                if (additions.Length > 0) await store.ReplaceAssignmentsAsync(agent.Id, currentAssignments.Concat(additions).ToArray(), cancellationToken);
            }
            agents.Add(agent);
        }

        var team = new TeamDefinition(Guid.NewGuid(), NexusFeatureTeam.Name, NexusFeatureTeam.Description, true, agents.Select(agent => agent.Id).ToArray(), 1);
        await store.SaveTeamAsync(team, cancellationToken);
        return new ProvisionedFeatureTeam(team, agents);
    }

    public async Task<IReadOnlyList<AgentDefinition>> ProvisionDefaultCouncilAsync(DefaultCouncilProvisionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId)) throw new ArgumentException("A model ID is required.", nameof(request));
        var connection = await store.FindConnectionAsync(request.ConnectionId, cancellationToken) ?? throw new KeyNotFoundException("Model connection was not found.");
        if (!connection.Enabled) throw new InvalidOperationException("Model connection is disabled.");
        var existing = await store.ListAgentsAsync(cancellationToken);
        var featureOnlySkillKeys = new HashSet<string>(FeatureTeamSkills.Additional.Select(skill => skill.Key), StringComparer.OrdinalIgnoreCase);
        var defaults = (await store.ListSkillsAsync(cancellationToken)).Where(skill => skill.Enabled && skill.TrustLevel == "local-default" && !featureOnlySkillKeys.Contains(skill.Key)).OrderBy(skill => skill.Name).ToArray();
        var result = new List<AgentDefinition>();
        foreach (var template in DefaultCouncil.Agents)
        {
            var agent = existing.FirstOrDefault(value => value.Name == template.Name);
            if (agent is null)
            {
                var now = DateTimeOffset.UtcNow;
                agent = new AgentDefinition(Guid.NewGuid(), template.Name, template.Description, template.RoleId, template.Instructions, true, request.ModelId.Trim(), request.ConnectionId, 1, now, now, template.Name == "Execution Agent" ? AgentMode.Execution : AgentMode.Review);
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

    public async Task<WorkflowRun> RunWorkflowAsync(Guid workflowId, string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input is required.", nameof(input));
        var workflow = await store.FindWorkflowAsync(workflowId, cancellationToken) ?? throw new KeyNotFoundException("Workflow was not found.");
        if (!workflow.Enabled) throw new InvalidOperationException("Workflow is disabled.");
        var stages = await store.ListWorkflowStagesAsync(workflowId, cancellationToken); var edges = await store.ListWorkflowEdgesAsync(workflowId, cancellationToken);
        WorkflowValidator.Validate(workflow, stages, edges);
        var run = new WorkflowRun(Guid.NewGuid(), workflowId, input, RunStatus.Running, null, DateTimeOffset.UtcNow, null); await store.SaveWorkflowRunAsync(run, cancellationToken);
        try
        {
            var outputs = new Dictionary<Guid, string?>(); var pending = stages.ToDictionary(stage => stage.Id, stage => stage);
            while (pending.Count > 0)
            {
                var ready = pending.Values.Where(stage => edges.Where(edge => edge.ToStageId == stage.Id).All(edge => outputs.ContainsKey(edge.FromStageId))).OrderBy(stage => stage.Position).ToArray();
                if (ready.Length == 0) throw new InvalidOperationException("Workflow has no executable stage.");
                foreach (var stage in ready)
                {
                    if (stage.RequiresApproval) throw new UnauthorizedAccessException($"Workflow stage '{stage.Name}' requires an explicit approved action before it can execute.");
                    var checklist = string.Join("\n", JsonSerializer.Deserialize<string[]>(stage.ChecklistJson)!.Select(item => $"- [ ] {item}"));
                    var evidence = string.Join("\n---\n", edges.Where(edge => edge.ToStageId == stage.Id).Select(edge => outputs[edge.FromStageId]).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Length > 12000 ? value[..12000] : value));
                    var stageInput = $"Original request:\n{input}\n\nWorkflow stage: {stage.Name}\nChecklist:\n{checklist}\nExpected evidence: {stage.ExpectedEvidence}\n\nPrior-stage evidence:\n{evidence}\n\nDo not invent evidence. Explicitly mark each unchecked item and blocker.";
                    outputs[stage.Id] = (await RunAgentAsync(stage.AgentId, stageInput, cancellationToken)).Output; pending.Remove(stage.Id);
                }
            }
            run = run with { Status = RunStatus.Succeeded, FinishedAt = DateTimeOffset.UtcNow }; await store.SaveWorkflowRunAsync(run, cancellationToken); return run;
        }
        catch (OperationCanceledException)
        {
            await store.SaveWorkflowRunAsync(run with { Status = RunStatus.Failed, ErrorCode = "OperationCanceled", FinishedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run = run with { Status = RunStatus.Failed, ErrorCode = exception.GetType().Name, FinishedAt = DateTimeOffset.UtcNow }; await store.SaveWorkflowRunAsync(run, cancellationToken); throw;
        }
    }

    public async Task<EffectiveAgentConfiguration> GetEffectiveConfigurationAsync(Guid agentId, CancellationToken cancellationToken, bool quickChat = false)
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
        var grants = await store.ListToolGrantsAsync(agentId, cancellationToken);
        ValidateToolBackedSkills(skills, grants);

        var resolvedInstructions = new List<string>();
        foreach (var skill in skills.Where(x => !quickChat && x.Type == SkillType.Instruction))
        {
            if (skill.SourceType == "project-file" && skillContents is null) throw new InvalidOperationException("A skill-file resolver must be configured.");
            var content = skillContents is null ? skill.InstructionContent : await skillContents.ResolveAsync(skill, cancellationToken);
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
            resolvedInstructions.Add($"Skill: {skill.Key}\nSource: {skill.SourceRef}\nSHA256: {digest}\n{content}");
        }
        var instructionParts = new[] { "You are operating inside DragonKMG. Never bypass tool authorization.", ModeGuardrail(agent.Mode), role.DefaultInstructions, agent.Instructions, quickChat ? "Explicit quick conversation profile. Assigned skill bodies are not loaded. Answer briefly. Do not implement changes, produce patches, execute tools or claim tests ran. For implementation or specialist review, ask the user to resend in the full profile." : "" }
            .Concat(resolvedInstructions)
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return new EffectiveAgentConfiguration(agent, role, skills, assignments, grants, string.Join("\n\n", instructionParts));
    }

    public async Task<AgentRun> RunAgentAsync(Guid agentId, string input, CancellationToken cancellationToken, bool quickChat = false, bool allowLocalGreeting = false)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input is required.", nameof(input));
        var localReply = allowLocalGreeting ? ConversationFastPath.Reply(input) : null;
        var config = await GetEffectiveConfigurationAsync(agentId, cancellationToken, quickChat || localReply is not null);
        if (!config.Agent.Enabled) throw new InvalidOperationException("Agent is disabled.");
        var connection = await store.FindConnectionAsync(config.Agent.ConnectionId, cancellationToken) ?? throw new InvalidOperationException("Model connection was not found.");
        if (!connection.Enabled) throw new InvalidOperationException("Model connection is disabled.");
        if (connection.TrustZone == TrustZone.Cloud && !connection.CloudEgressAllowed) throw new UnauthorizedAccessException("Cloud egress is not authorized for this connection.");
        ConnectionPolicy.Validate(connection);
        var registeredModels = await store.ListConnectionModelsAsync(connection.Id, cancellationToken);
        var modelId = ModelIdentifier.Canonical(connection.Kind, config.Agent.ModelId);
        if (registeredModels.Any(model => ModelIdentifier.Canonical(connection.Kind, model.ModelId) == modelId && !model.Enabled)) throw new InvalidOperationException("The selected model is disabled.");
        var secret = string.IsNullOrWhiteSpace(connection.SecretReference) ? "" : secrets.Resolve(connection.SecretReference) ?? throw new InvalidOperationException("The configured secret reference is unavailable.");
        var started = DateTimeOffset.UtcNow;
        var effectiveInstructions = localReply is not null ? ConversationFastPath.SourceMarker + "\n" + config.Instructions : config.Instructions;
        var run = new AgentRun(Guid.NewGuid(), config.Agent.Id, config.Agent.Version, input, effectiveInstructions, RunStatus.Running, null, null, started, null);
        await store.SaveRunAsync(run, cancellationToken);
        try
        {
            var response = localReply is not null ? new ModelResponse(localReply, 0, 0) : await gateway.CompleteAsync(new ModelRequest(connection.Endpoint, secret, modelId, config.Instructions, input, cancellationToken, connection.Kind, quickChat ? 384 : null));
            var completed = run with { Status = RunStatus.Succeeded, Output = response.Text, FinishedAt = DateTimeOffset.UtcNow };
            await store.SaveRunAsync(completed, cancellationToken);
            return completed;
        }
        catch (OperationCanceledException)
        {
            await store.SaveRunAsync(run with { Status = RunStatus.Failed, ErrorCode = "OperationCanceled", FinishedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            throw;
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

    private static void ValidateToolBackedSkills(IEnumerable<SkillDefinition> skills, IReadOnlyList<AgentToolGrant> grants)
    {
        var granted = grants.Select(grant => grant.ToolId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills.Where(skill => skill.Type == SkillType.ToolBacked))
        {
            string[] required;
            try { required = JsonSerializer.Deserialize<string[]>(skill.RequiredToolIdsJson) ?? []; }
            catch (JsonException) { throw new InvalidOperationException($"Tool requirements for skill '{skill.Key}' are invalid."); }
            var missing = required.Where(toolId => !granted.Contains(toolId)).ToArray();
            if (missing.Length > 0) throw new UnauthorizedAccessException($"Skill '{skill.Key}' is blocked because tool grants are missing: {string.Join(", ", missing)}.");
        }
    }

    private static string ModeGuardrail(AgentMode mode) => mode switch
    {
        AgentMode.PlanOnly => "Mode: plan only. Provide scope, assumptions, ordered steps, risks, and acceptance checks. Do not provide source code, patches, commands, or claim implementation occurred.",
        AgentMode.QuestionOnly => "Mode: answer only. Explain, analyze, and ask precise clarification questions when needed. Do not provide source code, patches, commands, or claim implementation occurred.",
        AgentMode.Review => "Mode: review only. Assess supplied evidence and report verified findings, risks, and missing checks. Do not implement changes or claim execution occurred.",
        _ => "Mode: execution. Make only authorized changes and distinguish completed evidence from limitations."
    };
}
