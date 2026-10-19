using DragonKMG.Api;
using DragonKMG.Api.Authentication;
using DragonKMG.Infrastructure;
using DragonKMG.ToolGateway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DragonKMG.Tests;

public sealed class IntegrationHardeningTests
{
    [Fact]
    public async Task McpCallWithoutExplicitAgentGrantDoesNotMakeNetworkRequest()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);
        using var client = new McpStreamableHttpClient(http, new McpStaticRegistry(
            [new McpHttpServer("demo", new Uri("https://mcp.example/mcp"))], []));
        using var document = JsonDocument.Parse("{}");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.CallToolAsync(
            Guid.NewGuid().ToString(), "demo", "read", document.RootElement, default));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task McpRedirectIsRejectedBeforeFollowingLocation()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        {
            Headers = { Location = new Uri("https://attacker.example/collect") }
        });
        using var http = new HttpClient(handler);
        using var client = new McpStreamableHttpClient(http, new McpStaticRegistry(
            [new McpHttpServer("demo", new Uri("https://mcp.example/mcp"))], []));

        var exception = await Assert.ThrowsAsync<McpHttpException>(() => client.ListToolsAsync("demo"));

        Assert.Equal("redirect_blocked", exception.Code);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void McpRejectsNonLoopbackHttpEndpoints()
    {
        Assert.Throws<ArgumentException>(() => new McpStaticRegistry(
            [new McpHttpServer("unsafe", new Uri("http://mcp.example/tools"))], []));
    }

    [Fact]
    public void McpSchemaRejectsUnsupportedKeywordsAndExtraProperties()
    {
        Assert.Throws<McpHttpException>(() => McpInputSchemaValidator.ValidateSchema(
            "{\"type\":\"object\",\"properties\":{},\"$ref\":\"#/$defs/x\"}"));

        const string schema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},\"required\":[\"name\"],\"additionalProperties\":false}";
        using var invalidArguments = JsonDocument.Parse("{\"name\":\"safe\",\"extra\":true}");
        Assert.Throws<McpHttpException>(() => McpInputSchemaValidator.ValidateArguments(schema, invalidArguments.RootElement));
    }

    [Fact]
    public async Task BackupDoesNotCreateAnEmptySourceDatabaseWhenConfiguredFileIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragonkmg-backup-{Guid.NewGuid():N}");
        var database = Path.Combine(root, "missing.db");
        try
        {
            var runner = new SqliteBackupRunner(database, Path.Combine(root, "backups"));
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => runner.CreateAsync(default));
            Assert.False(File.Exists(database));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "backups")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BackupCreatesIntegrityCheckedSnapshotAndListsOnlyArtifactMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragonkmg-backup-{Guid.NewGuid():N}");
        var database = Path.Combine(root, "live.db");
        var directory = Path.Combine(root, "backups");
        try
        {
            Directory.CreateDirectory(root);
            await new SqlitePlatformStore(database).InitializeAsync(default);
            var runner = new SqliteBackupRunner(database, directory);
            var backup = await runner.CreateAsync(default);
            var listed = await runner.ListAsync(default);

            Assert.True(backup.SizeBytes > 0);
            Assert.Single(listed);
            Assert.Equal(backup.FileName, Assert.Single(listed).FileName);
            Assert.True(File.Exists(Path.Combine(directory, backup.FileName)));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionOidcFailsClosedWithoutRequiredScope()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Oidc:Enabled"] = "true",
            ["Authentication:Oidc:Authority"] = "https://identity.example",
            ["Authentication:Oidc:Issuer"] = "https://identity.example",
            ["Authentication:Oidc:Audience"] = "dragonkmg-api",
            ["Authentication:Oidc:RequireHttpsMetadata"] = "true"
        }).Build();
        var environment = new TestHostEnvironment("Production");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddDragonKmgOidcAuthentication(configuration, environment));

        Assert.Contains("RequiredScope", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class TestHostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "DragonKMG.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
