namespace DragonKMG.Domain;

public enum ConnectionKind { OpenAiCompatible, Ollama, CustomHttp, Anthropic, Gemini }
public enum TrustZone { Local, Cloud, RemoteSelfHosted }
public enum RunStatus { Queued, Running, Succeeded, Failed, Blocked }
public enum SkillType { Instruction, ToolBacked, Workflow, Imported }
public enum AgentMode { Execution, PlanOnly, QuestionOnly, Review }
public enum ApprovalStatus { Pending, Approved, Rejected, Expired }

public sealed record ProviderConnection(
    Guid Id, string Name, ConnectionKind Kind, Uri Endpoint, string SecretReference,
    bool Enabled, TrustZone TrustZone, bool CloudEgressAllowed, DateTimeOffset CreatedAt);

public sealed record AgentRole(Guid Id, string Name, string Description, string DefaultInstructions, bool IsSystem, int Version);
public sealed record ConnectionModel(Guid ConnectionId, string ModelId, string DisplayName, bool Enabled, bool IsDefault, string Source, DateTimeOffset UpdatedAt);

public sealed record SkillDefinition(
    Guid Id, string Key, string Name, string Description, int Version, bool Enabled,
    SkillType Type, string InstructionContent, string RequiredToolIdsJson, string TrustLevel,
    string SourceType = "user", string SourceRef = "");

public sealed record AgentSkillAssignment(Guid AgentId, Guid SkillId, bool Enabled, int Priority, string ConfigurationJson, int Version);
public sealed record AgentToolGrant(Guid AgentId, string ToolId, bool RequiresApproval, int Version);
public sealed record AuditEvent(Guid Id, DateTimeOffset OccurredAt, string Actor, string Action, int StatusCode, string Outcome);
public sealed record TerminalSession(Guid Id, string Name, Guid? SelectedAgentId, DateTimeOffset CreatedAt, DateTimeOffset LastOpenedAt);
public sealed record SessionMessage(Guid Id, Guid SessionId, string Role, string Content, Guid? AgentRunId, DateTimeOffset CreatedAt);
public enum InteractiveQuestionStatus { Pending, Answered, Expired, Cancelled }
public sealed record InteractiveQuestion(Guid Id, Guid SessionId, Guid? WorkflowRunId, Guid? AgentId, string Prompt, string OptionsJson, InteractiveQuestionStatus Status, string? Answer, DateTimeOffset CreatedAt, DateTimeOffset? AnsweredAt, DateTimeOffset ExpiresAt);
public sealed record SessionContextSnapshot(Guid Id, Guid SessionId, int Version, string Summary, string DecisionsJson, string OpenTasksJson, string RelevantFilesJson, int TokenCount, DateTimeOffset CreatedAt, Guid? ParentSnapshotId = null);
public sealed record SessionLink(Guid Id, Guid FromSessionId, Guid ToSessionId, string Relation, Guid? SnapshotId, DateTimeOffset CreatedAt);
public sealed record SessionGraph(IReadOnlyList<TerminalSession> Sessions, IReadOnlyList<SessionLink> Links, IReadOnlyList<SessionContextSnapshot> Snapshots);
public sealed record SessionContextItem(Guid Id, Guid SessionId, Guid? SnapshotId, string Kind, string Content, string SourceMessageIdsJson, int Priority, DateTimeOffset CreatedAt);
public sealed record SessionTokenUsage(Guid Id, Guid SessionId, Guid? AgentRunId, string ModelId, int InputTokens, int OutputTokens, decimal EstimatedCost, int ContextVersion, DateTimeOffset RecordedAt);
public sealed record SessionCheckpoint(Guid Id, Guid SessionId, int Sequence, string Label, string ContextJson, Guid? SnapshotId, DateTimeOffset CreatedAt);
public sealed record ApprovalRequest(Guid Id, Guid? AgentRunId, string Action, string Reason, ApprovalStatus Status, DateTimeOffset RequestedAt, DateTimeOffset ExpiresAt, DateTimeOffset? ResolvedAt);
public sealed record WorkflowDefinition(Guid Id, string Name, string Description, bool Enabled, int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record WorkflowStage(Guid Id, Guid WorkflowId, string Name, Guid AgentId, int Position, string ChecklistJson, string ExpectedEvidence, bool RequiresApproval);
public sealed record WorkflowEdge(Guid WorkflowId, Guid FromStageId, Guid ToStageId);
public sealed record WorkflowRun(Guid Id, Guid WorkflowId, string Input, RunStatus Status, string? ErrorCode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

public sealed record AgentDefinition(
    Guid Id, string Name, string Description, Guid RoleId, string Instructions, bool Enabled,
    string ModelId, Guid ConnectionId, int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, AgentMode Mode = AgentMode.Execution);

public sealed record AgentRun(
    Guid Id, Guid AgentId, int AgentVersion, string Input, string EffectiveInstructions,
    RunStatus Status, string? Output, string? ErrorCode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

public sealed record EffectiveAgentConfiguration(
    AgentDefinition Agent, AgentRole Role, IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<AgentSkillAssignment> Assignments, IReadOnlyList<AgentToolGrant> ToolGrants, string Instructions);
