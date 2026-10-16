using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Api;

public static class ProjectHistoryRoutes
{
    public static void MapProjectHistoryRoutes(this WebApplication app)
    {
        app.MapGet("/api/projects", async (IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListProjectsAsync(ct)));
        app.MapPost("/api/projects", async (CreateProjectRequest value, IPlatformStore store, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var project = new WorkspaceProject(Guid.NewGuid(), value.Name, value.RootPath, now, now);
            try
            {
                ProjectPolicy.ValidateFolderSelection(project);
                await store.SaveProjectAsync(project, ct);
                return Results.Created($"/api/projects/{project.Id}", project);
            }
            catch (ProjectFolderUnavailableException) { return Results.BadRequest(new { code = "ProjectFolderUnavailable" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidConfiguration" }); }
        });
        app.MapGet("/api/sessions/{id:guid}/project", async (Guid id, IPlatformStore store, CancellationToken ct) =>
        {
            if (!await SessionExistsAsync(id, store, ct)) return Results.NotFound(new { code = "SessionUnavailable" });
            return Results.Ok(new SessionProjectRequest(await store.GetSessionProjectAsync(id, ct)));
        });
        app.MapPut("/api/sessions/{id:guid}/project", async (Guid id, SessionProjectRequest value, IPlatformStore store, CancellationToken ct) =>
        {
            if (!await SessionExistsAsync(id, store, ct)) return Results.NotFound(new { code = "SessionUnavailable" });
            if (value.ProjectId is Guid project && !(await store.ListProjectsAsync(ct)).Any(item => item.Id == project)) return Results.BadRequest(new { code = "ProjectUnavailable" });
            var owner = Guid.NewGuid();
            if (!await store.TryAcquireSessionLeaseAsync(id, owner, DateTimeOffset.UtcNow.AddMinutes(1), ct)) return Results.Conflict(new { code = "SessionBusy" });
            try
            {
                await store.SetSessionProjectAsync(id, value.ProjectId, ct);
                return Results.Ok(value);
            }
            finally { await store.ReleaseSessionLeaseAsync(id, owner, CancellationToken.None); }
        });
        app.MapGet("/api/sessions/{id:guid}/history", async (Guid id, string? before, int? limit, IPlatformStore store, CancellationToken ct) =>
        {
            if (!await SessionExistsAsync(id, store, ct)) return Results.NotFound(new { code = "SessionUnavailable" });
            try { return Results.Ok(await store.GetSessionHistoryAsync(id, before, limit ?? 100, ct)); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "InvalidHistoryCursor" }); }
        });
    }

    private static async Task<bool> SessionExistsAsync(Guid id, IPlatformStore store, CancellationToken ct) => (await store.ListTerminalSessionsAsync(ct)).Any(session => session.Id == id);
}

public sealed record CreateProjectRequest(string Name, string RootPath);
public sealed record SessionProjectRequest(Guid? ProjectId);
