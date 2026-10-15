namespace DragonKMG.Domain;

public enum ConnectionKind { OpenAiCompatible, Ollama, CustomHttp }
public enum TrustZone { Local, Cloud, RemoteSelfHosted }
public enum RunStatus { Queued, Running, Succeeded, Failed, Blocked }
public enum SkillType { Instruction, ToolBacked, Workflow, Imported }
public enum AgentMode { Execution, PlanOnly, QuestionOnly, Review }

public sealed record ProviderConnection(
    Guid Id, string Name, ConnectionKind Kind, Uri Endpoint, string SecretReference,
    bool Enabled, TrustZone TrustZone, bool CloudEgressAllowed, DateTimeOffset CreatedAt);

public sealed record AgentRole(Guid Id, string Name, string Description, string DefaultInstructions, bool IsSystem, int Version);

public sealed record SkillDefinition(
    Guid Id, string Key, string Name, string Description, int Version, bool Enabled,
    SkillType Type, string InstructionContent, string RequiredToolIdsJson, string TrustLevel,
    string SourceType = "user", string SourceRef = "");

public sealed record AgentSkillAssignment(Guid AgentId, Guid SkillId, bool Enabled, int Priority, string ConfigurationJson, int Version);
public sealed record AgentToolGrant(Guid AgentId, string ToolId, bool RequiresApproval, int Version);

public sealed record AgentDefinition(
    Guid Id, string Name, string Description, Guid RoleId, string Instructions, bool Enabled,
    string ModelId, Guid ConnectionId, int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, AgentMode Mode = AgentMode.Execution);

public sealed record AgentRun(
    Guid Id, Guid AgentId, int AgentVersion, string Input, string EffectiveInstructions,
    RunStatus Status, string? Output, string? ErrorCode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

public sealed record EffectiveAgentConfiguration(
    AgentDefinition Agent, AgentRole Role, IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<AgentSkillAssignment> Assignments, IReadOnlyList<AgentToolGrant> ToolGrants, string Instructions);
