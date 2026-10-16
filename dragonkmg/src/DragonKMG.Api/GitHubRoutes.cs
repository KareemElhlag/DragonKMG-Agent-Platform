using DragonKMG.Application;
using Microsoft.AspNetCore.Mvc;
namespace DragonKMG.Api;

public static class GitHubRoutes
{
    public static void MapGitHubRoutes(this WebApplication app)
    {
        app.MapGet("/api/integrations/github", ([FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => Results.Ok(await service.StatusAsync(ct))));
        app.MapPost("/api/integrations/github", (GitHubConnectRequest input, [FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => Results.Ok(await service.ConnectAsync(input.Token, ct))));
        app.MapDelete("/api/integrations/github", ([FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => { await service.DisconnectAsync(ct); return Results.NoContent(); }));
        app.MapGet("/api/integrations/github/repositories", (int? page, [FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => Results.Ok(await service.RepositoriesAsync(page ?? 1, ct))));
        app.MapGet("/api/projects/{id:guid}/github", (Guid id, [FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => Results.Ok(new { binding = await service.BindingAsync(id, ct) })));
        app.MapPut("/api/projects/{id:guid}/github", (Guid id, GitHubBindRequest input, [FromServices] IGitHubIntegration service, CancellationToken ct) => ExecuteAsync(async () => Results.Ok(await service.BindAsync(id, input.FullName, ct))));
    }
    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (GitHubIntegrationException exception) { return Results.Json(new { code = exception.Code }, statusCode: exception.Code switch { "ProjectUnavailable" => 404, "GitHubNotConnected" => 409, "GitHubTimeout" => 504, "GitHubUnavailable" => 502, "GitHubVaultUnsupported" => 501, _ => 400 }); }
    }
}
public sealed record GitHubConnectRequest(string Token);
public sealed record GitHubBindRequest(string FullName);
