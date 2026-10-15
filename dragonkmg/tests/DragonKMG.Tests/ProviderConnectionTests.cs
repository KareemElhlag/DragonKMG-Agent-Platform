using System.Net;
using System.Text;
using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;
using DragonKMG.ModelGateway;

namespace DragonKMG.Tests;

public sealed class ProviderConnectionTests
{
    [Theory]
    [InlineData("https://example.com", ConnectionKind.OpenAiCompatible, "chat/completions", "/v1/chat/completions")]
    [InlineData("https://example.com/proxy/v1", ConnectionKind.OpenAiCompatible, "models", "/proxy/v1/models")]
    [InlineData("https://example.com/v1/chat/completions", ConnectionKind.OpenAiCompatible, "models", "/v1/models")]
    [InlineData("http://localhost:11434", ConnectionKind.Ollama, "chat", "/api/chat")]
    [InlineData("https://example.com", ConnectionKind.Anthropic, "messages", "/v1/messages")]
    [InlineData("https://example.com/v1beta", ConnectionKind.Gemini, "models", "/v1beta/models")]
    public void NormalizesBaseEndpoints(string endpoint, ConnectionKind kind, string operation, string path) =>
        Assert.Equal(path, ProviderEndpoint.Build(new Uri(endpoint), kind, operation).AbsolutePath);

    [Theory]
    [InlineData("gemini-x")]
    [InlineData("models/gemini-x")]
    [InlineData("models/models/gemini-x")]
    public void GeminiCanonicalizationIsIdempotent(string value)
    {
        var canonical = ModelIdentifier.Canonical(ConnectionKind.Gemini, value);
        Assert.Equal("gemini-x", canonical);
        Assert.Equal(canonical, ModelIdentifier.Canonical(ConnectionKind.Gemini, canonical));
    }

    [Theory]
    [InlineData(ConnectionKind.Ollama, "{\"message\":{\"content\":\"OK\"},\"prompt_eval_count\":4,\"eval_count\":2}", "/api/chat", "")]
    [InlineData(ConnectionKind.Anthropic, "{\"content\":[{\"type\":\"text\",\"text\":\"OK\"}],\"usage\":{\"input_tokens\":4,\"output_tokens\":2}}", "/v1/messages", "x-api-key")]
    [InlineData(ConnectionKind.Gemini, "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"OK\"}]}}],\"usageMetadata\":{\"promptTokenCount\":4,\"candidatesTokenCount\":2}}", "/v1beta/models/test-model:generateContent", "x-goog-api-key")]
    public async Task NativeProtocolsUseExpectedPayloadAndHeaders(ConnectionKind kind, string body, string path, string keyHeader)
    {
        using var client = new HttpClient(new InspectHandler(async request =>
        {
            Assert.Equal(path, request.RequestUri!.AbsolutePath);
            Assert.DoesNotContain("test-secret", request.RequestUri.ToString());
            if (keyHeader.Length > 0) Assert.Equal("test-secret", request.Headers.GetValues(keyHeader).Single());
            else Assert.Null(request.Headers.Authorization);
            var payload = await request.Content!.ReadAsStringAsync();
            Assert.Contains("32", payload);
            Assert.Contains("hello", payload);
        }, body));
        var gateway = new OpenAiCompatibleGateway(client);
        var result = await gateway.CompleteAsync(new ModelRequest(new Uri("https://example.com"), keyHeader.Length == 0 ? "" : "test-secret", "test-model", "system", "hello", CancellationToken.None, kind, 32));
        Assert.Equal("OK", result.Text);
        Assert.Equal(4, result.InputTokens);
        Assert.Equal(2, result.OutputTokens);
    }

    [Theory]
    [InlineData(ConnectionKind.OpenAiCompatible, "{\"data\":[{\"id\":\"model-a\"},{\"id\":\"model-a\"}]}", "model-a")]
    [InlineData(ConnectionKind.Anthropic, "{\"data\":[{\"id\":\"claude-test\"}],\"has_more\":false}", "claude-test")]
    [InlineData(ConnectionKind.Ollama, "{\"models\":[{\"name\":\"llama:test\"}]}", "llama:test")]
    [InlineData(ConnectionKind.Gemini, "{\"models\":[{\"name\":\"models/gemini-test\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/embed\",\"supportedGenerationMethods\":[\"embedContent\"]}]}", "gemini-test")]
    public async Task DiscoversNativeCatalogs(ConnectionKind kind, string body, string expected)
    {
        using var client = new HttpClient(new InspectHandler(request => { Assert.Equal(HttpMethod.Get, request.Method); return Task.CompletedTask; }, body));
        var connection = new ProviderConnection(Guid.NewGuid(), "test", kind, new Uri("https://example.com"), "", true, TrustZone.RemoteSelfHosted, false, DateTimeOffset.UtcNow);
        var models = await new OpenAiCompatibleGateway(client).DiscoverAsync(connection, "", CancellationToken.None);
        Assert.Equal(new[] { expected }, models);
    }

    [Fact]
    public async Task RegistryPreservesManualMetadataAndCanonicalDisabledModels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-provider-{Guid.NewGuid():N}.db");
        try
        {
            var ct = CancellationToken.None;
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(ct);
            var connection = new ProviderConnection(Guid.NewGuid(), "test", ConnectionKind.Gemini, new Uri("https://example.com"), "", true, TrustZone.RemoteSelfHosted, false, DateTimeOffset.UtcNow);
            await store.SaveConnectionAsync(connection, ct);
            var first = new ConnectionModel(connection.Id, "models/models/gemini-a", "My name", false, false, "manual", DateTimeOffset.UtcNow);
            await store.SaveConnectionModelAsync(first, false, ct);
            await store.SaveConnectionModelAsync(first with { ModelId = "gemini-a", DisplayName = "discovered", Enabled = true }, true, ct);
            var saved = Assert.Single(await store.ListConnectionModelsAsync(connection.Id, ct));
            Assert.Equal("gemini-a", saved.ModelId);
            Assert.False(saved.Enabled);
            Assert.Equal("My name", saved.DisplayName);
            using var deniedClient = new HttpClient(new InspectHandler(_ => throw new Exception("Disabled model reached HTTP."), "{}"));
            var deniedGateway = new OpenAiCompatibleGateway(deniedClient);
            var service = new ProviderConnectionService(store, deniedGateway, deniedGateway, new NoSecrets());
            var denied = await Assert.ThrowsAsync<ProviderConfigurationException>(() => service.TestModelAsync(connection.Id, "models/models/gemini-a", ct));
            Assert.Equal("ModelDisabled", denied.Message);
            await store.SaveConnectionAsync(connection with { TrustZone = TrustZone.Cloud }, ct);
            var blocked = await Assert.ThrowsAsync<ProviderConfigurationException>(() => service.ProbeAsync(connection.Id, ct));
            Assert.Equal("CloudBlocked", blocked.Message);
            await store.SaveConnectionModelAsync(first with { ModelId = "a", Enabled = true, IsDefault = true }, false, ct);
            await store.SaveConnectionModelAsync(first with { ModelId = "b", Enabled = true, IsDefault = true }, false, ct);
            Assert.Equal("b", Assert.Single((await store.ListConnectionModelsAsync(connection.Id, ct)).Where(model => model.IsDefault)).ModelId);
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveConnectionModelAsync(first with { IsDefault = true }, false, ct));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    private sealed class NoSecrets : ISecretResolver
    {
        public string? Resolve(string reference) => null;
    }
    private sealed class InspectHandler(Func<HttpRequestMessage, Task> inspect, string body) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await inspect(request);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
