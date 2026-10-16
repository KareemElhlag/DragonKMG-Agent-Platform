namespace DragonKMG.Application;

public sealed record GitHubStatus(bool Connected, string? Login, bool VaultSupported);
public sealed record GitHubRepository(long Id, string FullName, bool Private, string HtmlUrl);
public sealed record GitHubBinding(Guid ProjectId, string FullName, DateTimeOffset BoundAt);
public sealed class GitHubIntegrationException(string code) : Exception(code) { public string Code { get; } = code; }
public interface IGitHubCredentialVault
{
    bool Supported { get; }
    string? Read();
    void Store(string token);
    void Remove();
}
public interface IGitHubIntegration
{
    Task<GitHubStatus> StatusAsync(CancellationToken ct);
    Task<GitHubStatus> ConnectAsync(string token, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task<IReadOnlyList<GitHubRepository>> RepositoriesAsync(int page, CancellationToken ct);
    Task<GitHubBinding?> BindingAsync(Guid projectId, CancellationToken ct);
    Task<GitHubBinding> BindAsync(Guid projectId, string fullName, CancellationToken ct);
}
