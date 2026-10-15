using DragonKMG.Application;

namespace DragonKMG.Api;

public static class SessionConversationRoutes
{
    public static void MapSessionConversationRoutes(this WebApplication app)
    {
        app.MapPost("/api/sessions/{id:guid}/messages", async (Guid id, SessionPrompt request, SessionConversationService service, CancellationToken ct) =>
        {
            try
            {
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                var run = await service.SendAsync(id, request.AgentId, request.Input, ct, request.QuickChat);
                return Results.Ok(new { runId = run.Id, output = run.Output, elapsedMs = elapsed.ElapsedMilliseconds, instructionCharacters = run.EffectiveInstructions.Length, source = run.EffectiveInstructions.StartsWith(ConversationFastPath.SourceMarker, StringComparison.Ordinal) ? "local" : "provider" });
            }
            catch (SessionBusyException) { return Results.Json(new { code = "SessionBusy" }, statusCode: 409); }
            catch (KeyNotFoundException) { return Results.NotFound(new { code = "SessionUnavailable" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
            catch (UnauthorizedAccessException) { return Results.Json(new { code = "CloudBlocked" }, statusCode: 403); }
            catch (OperationCanceledException) { return Results.Json(new { code = "ProviderTimeout" }, statusCode: 504); }
            catch (Exception) { return Results.Json(new { code = "ProviderFailed" }, statusCode: 502); }
        });
    }
}
public sealed record SessionPrompt(Guid AgentId, string Input, bool QuickChat = false);
