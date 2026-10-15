namespace DragonKMG.Domain;

public enum PlatformPermission { ManageAgents, ManageSkills, ManageConnections, RunWorkflows, ReviewAudit, ManageIdentity }

public sealed record Workspace(Guid Id, string Name, DateTimeOffset CreatedAt);
public sealed record PlatformUser(Guid Id, Guid WorkspaceId, string UserName, string DisplayName, bool Enabled, DateTimeOffset CreatedAt);
public sealed record UserRoleAssignment(Guid UserId, string RoleKey, DateTimeOffset GrantedAt);
public sealed record PublicKeyDescriptor(Guid Id, Guid WorkspaceId, string KeyId, string Algorithm, string PublicKeyPem, DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt);
