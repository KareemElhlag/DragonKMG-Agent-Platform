using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Api;

public static class ProviderConnectionRoutes
{
    public static void MapProviderConnectionRoutes(this WebApplication app)
    {
        app.MapGet("/api/models", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListConnectionModelsAsync(null, ct)));
        app.MapGet("/api/connections/{id:guid}/models", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListConnectionModelsAsync(id, ct)));
        app.MapPost("/api/connections/configure", (ConnectionConfiguration request, IPlatformStore store, WindowsDpapiSecretVault vault, CancellationToken ct) => Safe(async () =>
        {
            var connection = request.Connection;
            if (!string.IsNullOrEmpty(request.ApiKey))
            {
                if (!OperatingSystem.IsWindows()) return Results.Json(new { code = "VaultUnavailable" }, statusCode: 501);
                if (string.IsNullOrWhiteSpace(request.ApiKey) || request.ApiKey.Length > 8192 || request.ApiKey.Any(char.IsControl)) return Results.BadRequest(new { code = "InvalidConfiguration" });
                var vaultName = "provider-" + Guid.NewGuid().ToString("N");
                connection = connection with { SecretReference = "vault:" + vaultName };
                ConnectionPolicy.Validate(connection);
                vault.Store(vaultName, request.ApiKey.Trim());
            }
            else ConnectionPolicy.Validate(connection);
            await store.SaveConnectionAsync(connection, ct);
            return Results.Ok(connection);
        }));
        app.MapPut("/api/connections/{id:guid}/models", (Guid id, ConnectionModel model, IPlatformStore store, CancellationToken ct) => Safe(async () =>
        {
            if (model.ConnectionId != id || await store.FindConnectionAsync(id, ct) is null) return Results.BadRequest(new { code = "InvalidConfiguration" });
            await store.SaveConnectionModelAsync(model with { Source = "manual", UpdatedAt = DateTimeOffset.UtcNow }, false, ct);
            return Results.Ok(await store.ListConnectionModelsAsync(id, ct));
        }));
        app.MapPost("/api/connections/{id:guid}/discover", (Guid id, ProviderConnectionService service, CancellationToken ct) => Safe(async () => Results.Ok(await service.DiscoverAsync(id, ct))));
        app.MapPost("/api/connections/{id:guid}/test", (Guid id, ProviderConnectionService service, CancellationToken ct) => Safe(async () =>
        {
            var models = await service.ProbeAsync(id, ct);
            return Results.Ok(new { success = true, modelCount = models.Count });
        }));
        app.MapPost("/api/connections/{id:guid}/test-model", (Guid id, ModelTest request, ProviderConnectionService service, CancellationToken ct) => Safe(async () =>
        {
            var result = await service.TestModelAsync(id, request.ModelId, ct);
            return Results.Ok(new { success = true, request.ModelId, output = result.Text.Length > 300 ? result.Text[..300] : result.Text, result.InputTokens, result.OutputTokens });
        }));
    }
    private static async Task<IResult> Safe(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (ProviderConfigurationException exception) { return Results.Json(new { code = exception.Message }, statusCode: exception.Message == "CloudBlocked" ? 403 : 409); }
        catch (OperationCanceledException) { return Results.Json(new { code = "ProviderTimeout" }, statusCode: 504); }
        catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return Results.Json(new { code = "ProviderFailed" }, statusCode: 502);
        }
    }
}
public sealed record ConnectionConfiguration(ProviderConnection Connection, string? ApiKey);
public sealed record ModelTest(string ModelId);
