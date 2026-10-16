using System.Net;
using System.Text;
using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class GitHubIntegrationTests
{
    private const string Token = "github_pat_synthetic_test_only";
    [Fact]
    public async Task InvalidCredentialIsNeverPersistedAndUpstreamBodyIsNotExposed()
    {
        var vault = new FakeVault();
        using var fixture = new Fixture(vault, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret-upstream-body") });
        var error = await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.ConnectAsync(Token, default));
        Assert.Equal("GitHubTokenInvalid", error.Code); Assert.Null(vault.Value); Assert.DoesNotContain("secret", error.Message);
    }
    [Fact]
    public async Task ConnectListBindReadbackDisconnectUsesOnlyFixedReadOnlyRequests()
    {
        var vault = new FakeVault(); var requests = new List<string>();
        using var fixture = new Fixture(vault, request =>
        {
            if (request.Headers.Authorization?.Parameter == "github_pat_rejected") return new(HttpStatusCode.Unauthorized);
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("https", request.RequestUri!.Scheme); Assert.Equal("api.github.com", request.RequestUri.Host);
            Assert.Contains(request.Headers.Authorization!.Parameter, new[] { Token, "github_pat_second_account" }); requests.Add(request.RequestUri.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri.AbsolutePath == "/user" ? "{\"login\":\"tester\"}" : request.RequestUri.AbsolutePath == "/user/repos" ? "[" + Repository + "]" : Repository, Encoding.UTF8, "application/json") };
        });
        await fixture.Store.InitializeAsync(default);
        var now = DateTimeOffset.UtcNow; var project = new WorkspaceProject(Guid.NewGuid(), "Test", Path.GetTempPath(), now, now); await fixture.Store.SaveProjectAsync(project, default);
        Assert.True((await fixture.Service.ConnectAsync(Token, default)).Connected);
        Assert.Equal("tester/private", Assert.Single(await fixture.Service.RepositoriesAsync(1, default)).FullName);
        Assert.Equal("tester/private", (await fixture.Service.BindAsync(project.Id, "tester/private", default)).FullName);
        Assert.Equal(project.Id, (await fixture.Service.BindingAsync(project.Id, default))!.ProjectId);
        await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.ConnectAsync("github_pat_rejected", default));
        Assert.Equal(Token, vault.Value); Assert.NotNull(await fixture.Service.BindingAsync(project.Id, default));
        await fixture.Service.ConnectAsync("github_pat_second_account", default);
        Assert.Null(await fixture.Service.BindingAsync(project.Id, default));
        await fixture.Service.DisconnectAsync(default); Assert.Null(vault.Value); Assert.Null(await fixture.Service.BindingAsync(project.Id, default));
        Assert.Equal(4, requests.Count);
    }
    [Theory]
    [InlineData("ghp_not_allowed")]
    [InlineData("github_pat_bad token")]
    [InlineData("")]
    public async Task RejectsNonFineGrainedOrMalformedTokensBeforeNetwork(string token)
    {
        using var fixture = new Fixture(new FakeVault(), _ => throw new Exception("Must not call"));
        Assert.Equal("GitHubTokenInvalid", (await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.ConnectAsync(token, default))).Code);
    }
    [Fact]
    public async Task EnforcesPageAndResponseBounds()
    {
        using var fixture = new Fixture(new FakeVault { Value = Token }, _ => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 512 * 1024 + 1)) });
        Assert.Equal("GitHubPageInvalid", (await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.RepositoriesAsync(101, default))).Code);
        Assert.Equal("GitHubResponseTooLarge", (await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.RepositoriesAsync(1, default))).Code);
    }
    [Fact]
    public async Task UnsupportedVaultFailsClosedWithoutNetwork()
    {
        using var fixture = new Fixture(new FakeVault { Supported = false }, _ => throw new Exception("Must not call"));
        Assert.False((await fixture.Service.StatusAsync(default)).VaultSupported);
        Assert.Equal("GitHubVaultUnsupported", (await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.ConnectAsync(Token, default))).Code);
    }
    [Fact]
    public async Task InFlightOldAccountBindingCannotExposeNamesAfterCredentialSwitch()
    {
        var vault = new FakeVault { Value = Token };
        using var fixture = new Fixture(vault, request =>
        {
            Assert.Equal(Token, request.Headers.Authorization!.Parameter);
            vault.Value = "github_pat_second_account"; // Another process replaces credentials while A's HTTP call is in flight.
            return new(HttpStatusCode.OK) { Content = new StringContent(Repository) };
        });
        await fixture.Store.InitializeAsync(default);
        var now = DateTimeOffset.UtcNow; var project = new WorkspaceProject(Guid.NewGuid(), "Scope", Path.GetTempPath(), now, now); await fixture.Store.SaveProjectAsync(project, default);
        Assert.Equal("GitHubAccountChanged", (await Assert.ThrowsAsync<GitHubIntegrationException>(() => fixture.Service.BindAsync(project.Id, "tester/private", default))).Code);
        Assert.Null(await fixture.Service.BindingAsync(project.Id, default));
    }
    private const string Repository = "{\"id\":1,\"full_name\":\"tester/private\",\"private\":true,\"html_url\":\"https://github.com/tester/private\"}";
    private sealed class FakeVault : IGitHubCredentialVault { public bool Supported { get; set; } = true; public string? Value; public string? Read() => Value; public void Store(string token) => Value = token; public void Remove() => Value = null; }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request)); }
    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "dragon-github-" + Guid.NewGuid().ToString("N") + ".db");
        private readonly HttpClient _client;
        public SqlitePlatformStore Store { get; }
        public GitHubIntegration Service { get; }
        public Fixture(IGitHubCredentialVault vault, Func<HttpRequestMessage, HttpResponseMessage> handler) { _client = new(new Handler(handler)); Store = new(_path); Service = new(_client, vault, Store, _path); }
        public void Dispose() { _client.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(_path); }
    }
}
