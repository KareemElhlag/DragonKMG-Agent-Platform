using System.Globalization;
using System.Text;
using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Infrastructure;

public sealed partial class SqlitePlatformStore
{
    public async Task<IReadOnlyList<WorkspaceProject>> ListProjectsAsync(CancellationToken ct) => await ReadManyAsync("SELECT * FROM workspace_projects ORDER BY name,id", r => new WorkspaceProject(Id(r, "id"), r.GetString(1), r.GetString(2), Time(r, "created_at"), Time(r, "updated_at")), ct);

    public async Task SaveProjectAsync(WorkspaceProject project, CancellationToken ct)
    {
        ProjectPolicy.Validate(project);
        await ExecuteAsync("INSERT INTO workspace_projects VALUES($id,$name,$root,$created,$updated)", [("$id", project.Id), ("$name", project.Name), ("$root", project.RootPath), ("$created", project.CreatedAt), ("$updated", project.UpdatedAt)], ct);
    }

    public async Task<Guid?> GetSessionProjectAsync(Guid sessionId, CancellationToken ct) => (await ReadManyAsync("SELECT project_id FROM session_projects WHERE session_id=$id AND project_id IS NOT NULL", r => Guid.Parse(r.GetString(0)), ct, ("$id", sessionId))).Select(id => (Guid?)id).FirstOrDefault();

    public async Task SetSessionProjectAsync(Guid sessionId, Guid? projectId, CancellationToken ct) => await ExecuteAsync("INSERT INTO session_projects VALUES($session,$project) ON CONFLICT(session_id) DO UPDATE SET project_id=excluded.project_id", [("$session", sessionId), ("$project", projectId)], ct);

    public async Task<SessionHistoryPage> GetSessionHistoryAsync(Guid sessionId, string? before, int limit, CancellationToken ct)
    {
        var (time, messageId) = DecodeCursor(before);
        var size = Math.Clamp(limit, 1, 100);
        var rows = await ReadManyAsync("SELECT * FROM session_messages WHERE session_id=$session AND ($time IS NULL OR created_at<$time OR (created_at=$time AND id<$message)) ORDER BY created_at DESC,id DESC LIMIT $limit", ReadSessionMessage, ct, ("$session", sessionId), ("$time", time), ("$message", messageId), ("$limit", size + 1));
        var page = rows.Take(size).ToArray();
        var more = rows.Count > size;
        var cursor = more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{page[^1].CreatedAt:O}|{page[^1].Id:D}")) : null;
        return new SessionHistoryPage(page.Reverse().ToArray(), more, cursor);
    }

    private static (DateTimeOffset? Time, Guid? MessageId) DecodeCursor(string? cursor)
    {
        if (cursor is null) return (null, null);
        if (cursor.Length > 256) throw new ArgumentException("Invalid history cursor.");
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2 || !DateTimeOffset.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) || !Guid.TryParseExact(parts[1], "D", out var id)) throw new ArgumentException("Invalid history cursor.");
            return (time, id);
        }
        catch (FormatException) { throw new ArgumentException("Invalid history cursor."); }
    }
}
