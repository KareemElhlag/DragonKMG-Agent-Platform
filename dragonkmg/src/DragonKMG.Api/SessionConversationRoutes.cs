using DragonKMG.Application;

namespace DragonKMG.Api;

public static class SessionConversationRoutes
{
    public static void MapSessionConversationRoutes(this WebApplication app)
    {
        app.MapGet("/api/sessions/{id:guid}/execution", async (Guid id, IPlatformStore store, SessionExecutionTracker tracker, CancellationToken ct) =>
            (await store.ListTerminalSessionsAsync(ct)).Any(value => value.Id == id) ? Results.Ok(new { execution = tracker.Get(id) }) : Results.NotFound(new { code = "SessionUnavailable" }));
        app.MapPost("/api/sessions/{id:guid}/messages", async (Guid id, SessionPrompt request, SessionConversationService service, SessionExecutionTracker tracker, CancellationToken ct) =>
        {
            try
            {
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                var run = await service.SendTeamAsync(id, request.TeamId, request.Input, ct, request.QuickChat, request.AllowFileEgress, request.FileApprovalScope, tracker.Observe(id, request.RequestId ?? Guid.NewGuid()));
                return Results.Ok(new { runId = run.Id, output = run.Output, elapsedMs = elapsed.ElapsedMilliseconds, instructionCharacters = run.EffectiveInstructions.Length, source = run.EffectiveInstructions.StartsWith(ConversationFastPath.SourceMarker, StringComparison.Ordinal) ? "local" : "provider" });
            }
            catch (SessionBusyException) { return Results.Json(new { code = "SessionBusy" }, statusCode: 409); }
            catch (FileContextException exception) { return Results.Json(new { code = exception.Code }, statusCode: exception.StatusCode); }
            catch (TeamRoutingException exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (KeyNotFoundException) { return Results.NotFound(new { code = "SessionUnavailable" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
            catch (UnauthorizedAccessException) { return Results.Json(new { code = "CloudBlocked" }, statusCode: 403); }
            catch (OperationCanceledException) { return Results.Json(new { code = "ProviderTimeout" }, statusCode: 504); }
            catch (Exception) { return Results.Json(new { code = "ProviderFailed" }, statusCode: 502); }
        });
        app.MapPost("/api/sessions/{id:guid}/files/preview", async (Guid id, FilePreviewRequest request, SessionConversationService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.PreviewFileAsync(id, request.TeamId, request.Path, ct, request.Input)); }
            catch (FileContextException exception) { return Results.Json(new { code = exception.Code }, statusCode: exception.StatusCode); }
            catch (TeamRoutingException exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (KeyNotFoundException) { return Results.NotFound(new { code = "SessionUnavailable" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
        });
        app.MapPost("/api/sessions/{id:guid}/files/approval-context", async (Guid id, FileApprovalContextRequest request, SessionConversationService service, CancellationToken ct) =>
        {
            try { return Results.Ok(new { scope = await service.GetFileApprovalScopeAsync(id, request.TeamId, request.Input, ct) }); }
            catch (FileContextException exception) { return Results.Json(new { code = exception.Code }, statusCode: exception.StatusCode); }
            catch (TeamRoutingException exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (KeyNotFoundException) { return Results.NotFound(new { code = "SessionUnavailable" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
        });
    }
}
public sealed record SessionPrompt(Guid? AgentId, string Input, bool QuickChat = false, Guid? TeamId = null, bool AllowFileEgress = false, string? FileApprovalScope = null, Guid? RequestId = null);
public sealed record FilePreviewRequest(string Path, Guid? TeamId = null, bool AllowFileEgress = false, string? Input = null);
public sealed record FileApprovalContextRequest(string Input, Guid? TeamId = null);
