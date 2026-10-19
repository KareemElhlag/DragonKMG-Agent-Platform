using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Api;

public static class SessionGraphRoutes
{
    public static void MapSessionGraphRoutes(this WebApplication app)
    {
        app.MapGet("/api/sessions/{id:guid}/context/snapshots", async (Guid id, int? limit, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListContextSnapshotsAsync(id, limit ?? 20, ct)));
        app.MapPost("/api/sessions/{id:guid}/context/snapshots", async (Guid id, SessionContextSnapshotRequest request, IPlatformStore store, CancellationToken ct) =>
        {
            var snapshot = new SessionContextSnapshot(Guid.NewGuid(), id, request.Sequence, request.Summary, request.DecisionsJson ?? "[]", request.OpenTasksJson ?? "[]", request.RelevantFilesJson ?? "[]", request.TokenCount, DateTimeOffset.UtcNow, request.ParentSnapshotId);
            await store.SaveContextSnapshotAsync(snapshot, ct);
            return Results.Created($"/api/sessions/{id}/context/snapshots/{snapshot.Id}", snapshot);
        });
        app.MapPost("/api/sessions/{id:guid}/links", async (Guid id, SessionLinkRequest request, IPlatformStore store, CancellationToken ct) =>
        {
            if (id != request.ToSessionId && id != request.FromSessionId) return Results.BadRequest(new { code = "LinkRouteMismatch" });
            var link = new SessionLink(Guid.NewGuid(), request.FromSessionId, request.ToSessionId, request.Relation, request.SnapshotId, DateTimeOffset.UtcNow);
            await store.SaveSessionLinkAsync(link, ct);
            return Results.Created($"/api/sessions/{id}/links/{link.Id}", link);
        });
        app.MapGet("/api/sessions/{id:guid}/graph", async (Guid id, int? depth, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.GetSessionGraphAsync(id, depth ?? 2, ct)));
    }
}

public sealed record SessionContextSnapshotRequest(Guid? ParentSnapshotId, int Sequence, string Summary, string? DecisionsJson, string? OpenTasksJson, string? RelevantFilesJson, int TokenCount);
public sealed record SessionLinkRequest(Guid FromSessionId, Guid ToSessionId, string Relation, Guid? SnapshotId);
