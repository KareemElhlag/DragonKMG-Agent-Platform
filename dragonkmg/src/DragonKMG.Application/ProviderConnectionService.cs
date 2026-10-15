using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class ProviderConfigurationException(string code) : Exception(code);
public sealed class ProviderConnectionService(IPlatformStore store, IProviderCatalog catalog, IModelGateway gateway, ISecretResolver secrets)
{
    private async Task<(ProviderConnection Connection, string Secret)> ResolveAsync(Guid id, CancellationToken ct)
    {
        var connection = await store.FindConnectionAsync(id, ct) ?? throw new ProviderConfigurationException("ConnectionUnavailable");
        ConnectionPolicy.Validate(connection);
        if (!connection.Enabled) throw new ProviderConfigurationException("ConnectionDisabled");
        if (connection.TrustZone == TrustZone.Cloud && !connection.CloudEgressAllowed) throw new ProviderConfigurationException("CloudBlocked");
        var secret = string.IsNullOrWhiteSpace(connection.SecretReference) ? "" : secrets.Resolve(connection.SecretReference) ?? throw new ProviderConfigurationException("SecretUnavailable");
        return (connection, secret);
    }
    public async Task<IReadOnlyList<string>> ProbeAsync(Guid id, CancellationToken ct)
    {
        var (connection, secret) = await ResolveAsync(id, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        return await catalog.DiscoverAsync(connection, secret, timeout.Token);
    }
    public async Task<IReadOnlyList<ConnectionModel>> DiscoverAsync(Guid id, CancellationToken ct)
    {
        var models = await ProbeAsync(id, ct);
        foreach (var model in models)
            await store.SaveConnectionModelAsync(new ConnectionModel(id, model, model, true, false, "discovered", DateTimeOffset.UtcNow), true, ct);
        return await store.ListConnectionModelsAsync(id, ct);
    }
    public async Task<ModelResponse> TestModelAsync(Guid id, string modelId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Length > 256 || modelId.Any(char.IsControl)) throw new ArgumentException("Invalid model ID.");
        var (connection, secret) = await ResolveAsync(id, ct);
        var configured = await store.ListConnectionModelsAsync(id, ct);
        modelId = ModelIdentifier.Canonical(connection.Kind, modelId);
        if (configured.Any(model => ModelIdentifier.Canonical(connection.Kind, model.ModelId) == modelId && !model.Enabled)) throw new ProviderConfigurationException("ModelDisabled");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        return await gateway.CompleteAsync(new ModelRequest(connection.Endpoint, secret, modelId, "You are testing an API connection. Answer briefly.", "Reply with OK.", timeout.Token, connection.Kind, 32));
    }
}
