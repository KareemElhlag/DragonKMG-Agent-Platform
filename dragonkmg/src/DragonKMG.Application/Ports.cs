using DragonKMG.Domain;

namespace DragonKMG.Application;

public interface IPlatformStore
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderConnection>> ListConnectionsAsync(CancellationToken cancellationToken);
    Task<ProviderConnection?> FindConnectionAsync(Guid id, CancellationToken cancellationToken);
    Task SaveConnectionAsync(ProviderConnection connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRole>> ListRolesAsync(CancellationToken cancellationToken);
    Task<AgentRole?> FindRoleAsync(Guid id, CancellationToken cancellationToken);
    Task SaveRoleAsync(AgentRole role, CancellationToken cancellationToken);
    Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken);
    Task<SkillDefinition?> FindSkillAsync(Guid id, CancellationToken cancellationToken);
    Task SaveSkillAsync(SkillDefinition skill, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken);
    Task<AgentDefinition?> FindAgentAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAgentAsync(AgentDefinition agent, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentSkillAssignment>> ListAssignmentsAsync(Guid agentId, CancellationToken cancellationToken);
    Task ReplaceAssignmentsAsync(Guid agentId, IReadOnlyList<AgentSkillAssignment> assignments, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentToolGrant>> ListToolGrantsAsync(Guid agentId, CancellationToken cancellationToken);
    Task ReplaceToolGrantsAsync(Guid agentId, IReadOnlyList<AgentToolGrant> grants, CancellationToken cancellationToken);
    Task SaveRunAsync(AgentRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRun>> ListRunsAsync(Guid agentId, CancellationToken cancellationToken);
    Task<int> RecoverInterruptedRunsAsync(DateTimeOffset recoveredAt, CancellationToken cancellationToken);
    Task SaveAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<TerminalSession>> ListTerminalSessionsAsync(CancellationToken cancellationToken);
    Task SaveTerminalSessionAsync(TerminalSession session, CancellationToken cancellationToken);
    Task SaveSessionMessageAsync(SessionMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionMessage>> ListSessionMessagesAsync(Guid sessionId, int limit, CancellationToken cancellationToken);
}

public interface ISecretResolver
{
    string? Resolve(string secretReference);
}

public sealed record ModelRequest(Uri Endpoint, string ApiKey, string ModelId, string Instructions, string Input, CancellationToken CancellationToken);
public sealed record ModelResponse(string Text, int? InputTokens, int? OutputTokens);

public interface IModelGateway { Task<ModelResponse> CompleteAsync(ModelRequest request); }
