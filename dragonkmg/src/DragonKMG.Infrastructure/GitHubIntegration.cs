using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using DragonKMG.Application;
using Microsoft.Data.Sqlite;

namespace DragonKMG.Infrastructure;

public sealed class GitHubCredentialVault(WindowsDpapiSecretVault vault) : IGitHubCredentialVault
{
    public bool Supported => OperatingSystem.IsWindows();
    public string? Read() => vault.Read("github-account");
    public void Store(string token) => vault.Store("github-account", token);
    public void Remove() => vault.Remove("github-account");
}

/// <summary>Read-only account integration; tokens never become model context or tool grants.</summary>
public sealed class GitHubIntegration(HttpClient client, IGitHubCredentialVault vault, IPlatformStore store, string databasePath) : IGitHubIntegration
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
    public async Task<GitHubStatus> StatusAsync(CancellationToken ct)
    {
        if (!vault.Supported) return new(false, null, false);
        var token = ReadToken();
        if (token is null) return new(false, null, true);
        using var data = await GetAsync("user", token, ct);
        return new(true, ReadLogin(data.RootElement), true);
    }
    public async Task<GitHubStatus> ConnectAsync(string token, CancellationToken ct)
    {
        if (!vault.Supported) throw new GitHubIntegrationException("GitHubVaultUnsupported");
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512 || token.Any(char.IsWhiteSpace) || !token.StartsWith("github_pat_", StringComparison.Ordinal)) throw new GitHubIntegrationException("GitHubTokenInvalid");
        using var data = await GetAsync("user", token, ct);
        var login = ReadLogin(data.RootElement);
        if (string.IsNullOrWhiteSpace(login)) throw new GitHubIntegrationException("GitHubResponseInvalid");
        await _gate.WaitAsync(ct);
        try
        {
            // Clear account-scoped repository metadata before replacing credentials. A vault failure
            // may require rebinding, but can never expose account A's private names under account B.
            await ClearBindingsAsync(ct);
            vault.Store(token);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException) { throw new GitHubIntegrationException("GitHubVaultUnavailable"); }
        finally { _gate.Release(); }
        return new(true, login, true);
    }
    public async Task DisconnectAsync(CancellationToken ct)
    {
        if (!vault.Supported) throw new GitHubIntegrationException("GitHubVaultUnsupported");
        await _gate.WaitAsync(ct);
        try { vault.Remove(); await using var db = await OpenAsync(ct); await using var command = db.CreateCommand(); command.CommandText = "DELETE FROM github_project_bindings"; await command.ExecuteNonQueryAsync(ct); }
        finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<GitHubRepository>> RepositoriesAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 100) throw new GitHubIntegrationException("GitHubPageInvalid");
        using var data = await GetAsync($"user/repos?per_page=30&page={page}&sort=updated&affiliation=owner,collaborator,organization_member", RequiredToken(), ct);
        if (data.RootElement.ValueKind != JsonValueKind.Array || data.RootElement.GetArrayLength() > 30) throw new GitHubIntegrationException("GitHubResponseInvalid");
        return data.RootElement.EnumerateArray().Select(ParseRepository).ToArray();
    }
    public async Task<GitHubBinding?> BindingAsync(Guid projectId, CancellationToken ct)
    {
        await EnsureProjectAsync(projectId, ct);
        var token = ReadToken();
        if (token is null) return null;
        await using var db = await OpenAsync(ct); await using var command = db.CreateCommand();
        command.CommandText = "SELECT full_name,bound_at FROM github_project_bindings WHERE project_id=$id AND credential_scope=$scope"; command.Parameters.AddWithValue("$id", projectId); command.Parameters.AddWithValue("$scope", Scope(token));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(projectId, reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture)) : null;
    }
    public async Task<GitHubBinding> BindAsync(Guid projectId, string fullName, CancellationToken ct)
    {
        await EnsureProjectAsync(projectId, ct);
        if (fullName is null || fullName.Length > 200 || !Regex.IsMatch(fullName, @"\A[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+\z") || fullName.Split('/')[1] is "." or "..") throw new GitHubIntegrationException("GitHubRepositoryInvalid");
        await _gate.WaitAsync(ct);
        try
        {
            var token = RequiredToken();
            using var data = await GetAsync("repos/" + fullName, token, ct);
            var repository = ParseRepository(data.RootElement);
            await using var db = await OpenAsync(ct); await using var command = db.CreateCommand();
            command.CommandText = "INSERT INTO github_project_bindings(project_id,full_name,bound_at,credential_scope) VALUES($id,$name,$at,$scope) ON CONFLICT(project_id) DO UPDATE SET full_name=excluded.full_name,bound_at=excluded.bound_at,credential_scope=excluded.credential_scope";
            command.Parameters.AddWithValue("$id", projectId); command.Parameters.AddWithValue("$name", repository.FullName); command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("$scope", Scope(token)); await command.ExecuteNonQueryAsync(ct);
        }
        finally { _gate.Release(); }
        return await BindingAsync(projectId, ct) ?? throw new GitHubIntegrationException("GitHubAccountChanged");
    }
    private string? ReadToken() { try { return vault.Read(); } catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException) { throw new GitHubIntegrationException("GitHubVaultUnavailable"); } }
    private string RequiredToken() => ReadToken() ?? throw new GitHubIntegrationException("GitHubNotConnected");
    private async Task EnsureProjectAsync(Guid id, CancellationToken ct) { if (!(await store.ListProjectsAsync(ct)).Any(project => project.Id == id)) throw new GitHubIntegrationException("ProjectUnavailable"); }
    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var db = new SqliteConnection(_connectionString); await db.OpenAsync(ct);
        await using var command = db.CreateCommand(); command.CommandText = "CREATE TABLE IF NOT EXISTS github_project_bindings(project_id TEXT PRIMARY KEY REFERENCES workspace_projects(id),full_name TEXT NOT NULL,bound_at TEXT NOT NULL,credential_scope TEXT NOT NULL DEFAULT '')"; await command.ExecuteNonQueryAsync(ct);
        await using var transaction = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        command.Transaction = transaction; command.CommandText = "PRAGMA table_info(github_project_bindings)";
        var scopeExists = false;
        await using (var reader = await command.ExecuteReaderAsync(ct)) { while (await reader.ReadAsync(ct)) if (reader.GetString(1) == "credential_scope") scopeExists = true; }
        if (!scopeExists) { command.CommandText = "ALTER TABLE github_project_bindings ADD COLUMN credential_scope TEXT NOT NULL DEFAULT ''"; await command.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct); return db;
    }
    private static string Scope(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task ClearBindingsAsync(CancellationToken ct)
    {
        await using var db = await OpenAsync(ct); await using var command = db.CreateCommand(); command.CommandText = "DELETE FROM github_project_bindings"; await command.ExecuteNonQueryAsync(ct);
    }
    private static string ReadLogin(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("login", out var login) || login.ValueKind != JsonValueKind.String || login.GetString() is not { Length: > 0 and <= 100 } name) throw new GitHubIntegrationException("GitHubResponseInvalid");
        return name;
    }
    private static GitHubRepository ParseRepository(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("id", out var id) || !id.TryGetInt64(out var number) || number <= 0 || !value.TryGetProperty("full_name", out var fullName) || fullName.ValueKind != JsonValueKind.String || fullName.GetString() is not { Length: > 0 and <= 200 } name || !Regex.IsMatch(name, @"\A[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+\z") || !value.TryGetProperty("private", out var privacy) || privacy.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new GitHubIntegrationException("GitHubResponseInvalid");
        return new(number, name, privacy.GetBoolean(), "https://github.com/" + name);
    }
    private async Task<JsonDocument> GetAsync(string path, string token, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.github.com/" + path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); request.Headers.UserAgent.ParseAdd("DragonKMG/1.0"); request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28"); request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("N"));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new GitHubIntegrationException(response.StatusCode switch { HttpStatusCode.Unauthorized => "GitHubTokenInvalid", HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => "GitHubAccessOrRateLimited", HttpStatusCode.NotFound => "GitHubRepositoryUnavailable", _ => "GitHubUnavailable" });
            const int max = 512 * 1024;
            if (response.Content.Headers.ContentLength > max) throw new GitHubIntegrationException("GitHubResponseTooLarge");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0) { if (bytes.Length + read > max) throw new GitHubIntegrationException("GitHubResponseTooLarge"); bytes.Write(buffer, 0, read); }
            return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new GitHubIntegrationException("GitHubTimeout"); }
        catch (HttpRequestException) { throw new GitHubIntegrationException("GitHubUnavailable"); }
        catch (JsonException) { throw new GitHubIntegrationException("GitHubResponseInvalid"); }
    }
}
