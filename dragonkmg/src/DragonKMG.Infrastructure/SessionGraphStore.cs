using DragonKMG.Application;
using DragonKMG.Domain;
using Microsoft.Data.Sqlite;

namespace DragonKMG.Infrastructure;

public sealed partial class SqlitePlatformStore
{
    public Task SaveSessionContextSnapshotAsync(SessionContextSnapshot value, CancellationToken ct) => SaveContextSnapshotAsync(value, ct);
    public Task<IReadOnlyList<SessionContextSnapshot>> ListSessionContextSnapshotsAsync(Guid sessionId, int limit, CancellationToken ct) => ListContextSnapshotsAsync(sessionId, limit, ct);
    private async Task EnsureSessionGraphSchemaAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS session_links (id TEXT PRIMARY KEY, from_session_id TEXT NOT NULL REFERENCES terminal_sessions(id), to_session_id TEXT NOT NULL REFERENCES terminal_sessions(id), relation TEXT NOT NULL, snapshot_id TEXT NULL REFERENCES session_context_snapshots(id), created_at TEXT NOT NULL, UNIQUE(from_session_id, to_session_id, relation));
            CREATE INDEX IF NOT EXISTS session_links_from ON session_links(from_session_id);
            CREATE INDEX IF NOT EXISTS session_links_to ON session_links(to_session_id);
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveSessionLinkAsync(SessionLink value, CancellationToken ct)
    {
        if (value.FromSessionId == value.ToSessionId) throw new ArgumentException("A session cannot link to itself.");
        if (string.IsNullOrWhiteSpace(value.Relation) || value.Relation.Length > 64) throw new ArgumentException("Link relation is invalid.");
        await EnsureSessionGraphSchemaAsync(ct);
        await ExecuteAsync("INSERT INTO session_links VALUES($id,$from,$to,$type,$snapshot,$created) ON CONFLICT(from_session_id,to_session_id,relation) DO UPDATE SET snapshot_id=excluded.snapshot_id,created_at=excluded.created_at", [
            ("$id", value.Id), ("$from", value.FromSessionId), ("$to", value.ToSessionId), ("$type", value.Relation), ("$snapshot", value.SnapshotId), ("$created", value.CreatedAt)], ct);
    }

    public async Task<SessionGraph> GetSessionGraphAsync(Guid sessionId, int depth, CancellationToken ct)
    {
        await EnsureSessionGraphSchemaAsync(ct);
        depth = Math.Clamp(depth, 0, 8);
        var ids = new HashSet<Guid> { sessionId };
        var frontier = new HashSet<Guid> { sessionId };
        var links = new List<SessionLink>();
        for (var level = 0; level <= depth && frontier.Count > 0; level++)
        {
            var next = new HashSet<Guid>();
            foreach (var id in frontier)
            {
                var edges = await ReadManyAsync("SELECT * FROM session_links WHERE from_session_id=$id OR to_session_id=$id", ReadSessionLink, ct, ("$id", id));
                foreach (var edge in edges)
                {
                    if (links.All(existing => existing.Id != edge.Id)) links.Add(edge);
                    var other = edge.FromSessionId == id ? edge.ToSessionId : edge.FromSessionId;
                    if (ids.Add(other)) next.Add(other);
                }
            }
            frontier = next;
        }
        var sessions = await ReadManyAsync("SELECT * FROM terminal_sessions WHERE id=$id", ReadTerminalSession, ct, ("$id", sessionId));
        foreach (var id in ids.Where(id => id != sessionId)) sessions = sessions.Concat(await ReadManyAsync("SELECT * FROM terminal_sessions WHERE id=$id", ReadTerminalSession, ct, ("$id", id))).ToArray();
        var snapshots = new List<SessionContextSnapshot>();
        foreach (var id in ids) snapshots.AddRange(await ListContextSnapshotsAsync(id, 20, ct));
        return new SessionGraph(sessions, links, snapshots);
    }

    public async Task SaveContextSnapshotAsync(SessionContextSnapshot value, CancellationToken ct) => await ExecuteAsync("INSERT INTO session_context_snapshots VALUES($id,$session,$version,$summary,$decisions,$tasks,$files,$tokens,$created,$parent) ON CONFLICT(id) DO UPDATE SET summary=excluded.summary", [("$id",value.Id),("$session",value.SessionId),("$version",value.Version),("$summary",value.Summary),("$decisions",value.DecisionsJson),("$tasks",value.OpenTasksJson),("$files",value.RelevantFilesJson),("$tokens",value.TokenCount),("$created",value.CreatedAt),("$parent",value.ParentSnapshotId)], ct);
    public async Task<IReadOnlyList<SessionContextSnapshot>> ListContextSnapshotsAsync(Guid sessionId, int limit, CancellationToken ct) => await ReadManyAsync("SELECT * FROM session_context_snapshots WHERE session_id=$session ORDER BY version DESC LIMIT $limit", ReadSessionSnapshot, ct, ("$session",sessionId),("$limit",Math.Clamp(limit,1,200)));
    private static SessionContextSnapshot ReadSessionSnapshot(Microsoft.Data.Sqlite.SqliteDataReader r) => new(Id(r,"id"),Id(r,"session_id"),r.GetInt32(r.GetOrdinal("version")),r.GetString(r.GetOrdinal("summary")),r.GetString(r.GetOrdinal("decisions_json")),r.GetString(r.GetOrdinal("open_tasks_json")),r.GetString(r.GetOrdinal("relevant_files_json")),r.GetInt32(r.GetOrdinal("token_count")),Time(r,"created_at"),r.IsDBNull(r.GetOrdinal("parent_snapshot_id"))?null:Id(r,"parent_snapshot_id"));

}
