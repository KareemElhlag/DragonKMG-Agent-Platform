using DragonKMG.Domain;

namespace DragonKMG.Application;

/// <summary>Boundary for future multi-user identity without exposing private key material.</summary>
public interface IIdentityStore
{
    Task<IReadOnlyList<Workspace>> ListWorkspacesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> ListUsersAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SavePublicKeyAsync(PublicKeyDescriptor key, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicKeyDescriptor>> ListPublicKeysAsync(Guid workspaceId, CancellationToken cancellationToken);
}
