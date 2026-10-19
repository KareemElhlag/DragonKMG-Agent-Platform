using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Api;

public static class TeamRoutes
{
    public static void MapTeamRoutes(this WebApplication app)
    {
        app.MapGet("/api/teams/templates", async (PlatformService service, CancellationToken ct) =>
            Results.Ok(await service.GetFeatureTeamTemplatesAsync(ct)));
        app.MapPost("/api/teams/provision", async (FeatureTeamProvisionRequest request, PlatformService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.ProvisionFeatureTeamAsync(request, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(new { code = "TemplateOrConnectionUnavailable" }); }
            catch (FeatureTeamProvisionConflictException) { return Results.Conflict(new { code = "FeatureTeamProvisionConflict" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
            catch (InvalidOperationException) { return Results.UnprocessableEntity(new { code = "FeatureTeamPrerequisiteUnavailable" }); }
        });
        app.MapGet("/api/teams", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListTeamsAsync(ct)));
        app.MapPut("/api/teams/{id:guid}", async (Guid id, TeamDefinition value, IPlatformStore store, CancellationToken ct) =>
        {
            try
            {
                if (id != value.Id) throw new ArgumentException("Team id mismatch.");
                TeamPolicy.Validate(value);
                foreach (var agentId in value.AgentIds)
                    if (await store.FindAgentAsync(agentId, ct) is null) throw new ArgumentException("Unknown member.");
                await store.SaveTeamAsync(value, ct);
                return Results.Ok(value);
            }
            catch (TeamConflictException) { return Results.Conflict(new { code = "TeamConflict" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
        });
        app.MapGet("/api/sessions/{id:guid}/team", async (Guid id, IPlatformStore store, CancellationToken ct) =>
        {
            if (!(await store.ListTerminalSessionsAsync(ct)).Any(session => session.Id == id)) return Results.NotFound(new { code = "SessionUnavailable" });
            return Results.Ok(new SessionTeamRequest(await store.GetSessionTeamAsync(id, ct)));
        });
        app.MapPut("/api/sessions/{id:guid}/team", async (Guid id, SessionTeamRequest value, IPlatformStore store, CancellationToken ct) =>
        {
            if (!(await store.ListTerminalSessionsAsync(ct)).Any(session => session.Id == id)) return Results.NotFound(new { code = "SessionUnavailable" });
            var owner = Guid.NewGuid();
            if (!await store.TryAcquireSessionLeaseAsync(id, owner, DateTimeOffset.UtcNow.AddMinutes(1), ct)) return Results.Conflict(new { code = "SessionBusy" });
            try
            {
                if (value.TeamId is Guid teamId) await TeamPolicy.FindAvailableAsync(store, teamId, ct);
                await store.SetSessionTeamAsync(id, value.TeamId, ct);
                return Results.Ok(value);
            }
            catch (TeamRoutingException exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
            finally { await store.ReleaseSessionLeaseAsync(id, owner, CancellationToken.None); }
        });
    }
}

public sealed record SessionTeamRequest(Guid? TeamId);
