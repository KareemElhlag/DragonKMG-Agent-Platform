using DragonKMG.Api;
using DragonKMG.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DragonKMG.Tests;

public sealed class GitHubRouteTests
{
    [Fact]
    public async Task RoutesResolveIntegrationFromServicesAndSerializeEmptyBinding()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IGitHubIntegration, DisconnectedGitHub>();
        await using var app = builder.Build();
        app.MapGitHubRoutes();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var status = await client.GetAsync("/api/integrations/github");
            status.EnsureSuccessStatusCode();
            Assert.Contains("\"connected\":false", await status.Content.ReadAsStringAsync());
            var binding = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/github");
            binding.EnsureSuccessStatusCode();
            Assert.Equal("{\"binding\":null}", await binding.Content.ReadAsStringAsync());
        }
        finally { await app.StopAsync(); }
    }
    private sealed class DisconnectedGitHub : IGitHubIntegration
    {
        public Task<GitHubStatus> StatusAsync(CancellationToken ct) => Task.FromResult(new GitHubStatus(false, null, true));
        public Task<GitHubBinding?> BindingAsync(Guid id, CancellationToken ct) => Task.FromResult<GitHubBinding?>(null);
        public Task<GitHubStatus> ConnectAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task DisconnectAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<GitHubRepository>> RepositoriesAsync(int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<GitHubBinding> BindAsync(Guid id, string name, CancellationToken ct) => throw new NotSupportedException();
    }
}
