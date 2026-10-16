using DragonKMG.Domain;

namespace DragonKMG.Application;

public interface IPlatformStore
{
    Task<IReadOnlyList<WorkspaceProject>> ListProjectsAsync(CancellationToken cancellationToken);
    Task SaveProjectAsync(WorkspaceProject project, CancellationToken cancellationToken);
    Task<Guid?> GetSessionProjectAsync(Guid sessionId, CancellationToken cancellationToken);
    Task SetSessionProjectAsync(Guid sessionId, Guid? projectId, CancellationToken cancellationToken);
    Task<SessionHistoryPage> GetSessionHistoryAsync(Guid sessionId, string? before, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamDefinition>> ListTeamsAsync(CancellationToken cancellationToken);
    Task SaveTeamAsync(TeamDefinition team, CancellationToken cancellationToken);
    Task<Guid?> GetSessionTeamAsync(Guid sessionId, CancellationToken cancellationToken);
    Task SetSessionTeamAsync(Guid sessionId, Guid? teamId, CancellationToken cancellationToken);
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderConnection>> ListConnectionsAsync(CancellationToken cancellationToken);
    Task<ProviderConnection?> FindConnectionAsync(Guid id, CancellationToken cancellationToken);
    Task SaveConnectionAsync(ProviderConnection connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectionModel>> ListConnectionModelsAsync(Guid? connectionId, CancellationToken cancellationToken);
    Task SaveConnectionModelAsync(ConnectionModel model, bool preserveExisting, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRole>> ListRolesAsync(CancellationToken cancellationToken);
    Task<AgentRole?> FindRoleAsync(Guid id, CancellationToken cancellationToken);
    Task SaveRoleAsync(AgentRole role, CancellationToken cancellationToken);
    Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken);
    Task<SkillDefinition?> FindSkillAsync(Guid id, CancellationToken cancellationToken);
    Task SaveSkillAsync(SkillDefinition skill, CancellationToken cancellationToken);
    Task<bool> TrySaveSkillVersionAsync(SkillDefinition skill, CancellationToken cancellationToken);
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
    Task<bool> TryAcquireSessionLeaseAsync(Guid sessionId, Guid owner, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task ReleaseSessionLeaseAsync(Guid sessionId, Guid owner, CancellationToken cancellationToken);
    Task SaveSessionMessageAsync(SessionMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionMessage>> ListSessionMessagesAsync(Guid sessionId, int limit, CancellationToken cancellationToken);
    Task SaveApprovalAsync(ApprovalRequest approval, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequest>> ListApprovalsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowDefinition>> ListWorkflowsAsync(CancellationToken cancellationToken);
    Task<WorkflowDefinition?> FindWorkflowAsync(Guid id, CancellationToken cancellationToken);
    Task SaveWorkflowAsync(WorkflowDefinition workflow, IReadOnlyList<WorkflowStage> stages, IReadOnlyList<WorkflowEdge> edges, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowStage>> ListWorkflowStagesAsync(Guid workflowId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowEdge>> ListWorkflowEdgesAsync(Guid workflowId, CancellationToken cancellationToken);
    Task SaveWorkflowRunAsync(WorkflowRun run, CancellationToken cancellationToken);
    Task<Guid?> GetDefaultConnectionAsync(CancellationToken cancellationToken);
    Task SetDefaultConnectionAsync(Guid? connectionId, CancellationToken cancellationToken);
}

public interface ISecretResolver
{
    string? Resolve(string secretReference);
}

public interface ISkillContentResolver
{
    Task<string> ResolveAsync(SkillDefinition skill, CancellationToken cancellationToken);
}

public sealed record WorkspaceFileContext(string RelativePath, string Content, int Bytes, string Sha256);
public interface IWorkspaceFileContextResolver
{
    Task<WorkspaceFileContext> ReadAsync(string projectRoot, string path, CancellationToken cancellationToken);
}
public sealed class FileContextException(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record ModelRequest(Uri Endpoint, string ApiKey, string ModelId, string Instructions, string Input, CancellationToken CancellationToken, ConnectionKind Kind = ConnectionKind.OpenAiCompatible, int? MaxOutputTokens = null);
public sealed record ModelResponse(string Text, int? InputTokens, int? OutputTokens);

public interface IModelGateway { Task<ModelResponse> CompleteAsync(ModelRequest request); }
public interface IProviderCatalog
{
    Task<IReadOnlyList<string>> DiscoverAsync(ProviderConnection connection, string secret, CancellationToken cancellationToken);
}
