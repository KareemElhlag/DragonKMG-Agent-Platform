namespace DragonKMG.Application;

/// <summary>Persistent context artifacts are reclaimed locally without a model call.</summary>
/// <remarks>
/// This is deliberately a pure planner: callers apply <see cref="ContextGcPlan.DeleteIds"/>
/// in one database transaction. It never deletes the current head or an ancestor needed to
/// reconstruct a retained checkpoint. Consequently cleanup has zero token/provider cost.
/// </remarks>
public enum ContextArtifactKind { Summary, Checkpoint, GraphNode }

public sealed record ContextArtifact(
    Guid Id,
    Guid SessionId,
    ContextArtifactKind Kind,
    DateTimeOffset CreatedAt,
    Guid? ParentId = null,
    bool Pinned = false,
    bool IsCurrentHead = false,
    long Bytes = 0);

public sealed record ContextRetentionPolicy(
    TimeSpan MinimumAge,
    int MaxArtifactsPerSession = 64,
    long MaxBytesPerSession = 4 * 1024 * 1024)
{
    public ContextRetentionPolicy Normalize() => this with
    {
        MinimumAge = MinimumAge < TimeSpan.Zero ? TimeSpan.Zero : MinimumAge,
        MaxArtifactsPerSession = Math.Max(1, MaxArtifactsPerSession),
        MaxBytesPerSession = Math.Max(1, MaxBytesPerSession)
    };
}

public sealed record ContextGcPlan(
    IReadOnlyList<Guid> DeleteIds,
    long ReclaimedBytes,
    int ProtectedCount,
    string Reason);

public sealed class ContextGarbageCollector
{
    public ContextGcPlan Plan(IEnumerable<ContextArtifact> source, DateTimeOffset now, ContextRetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(source);
        var p = policy.Normalize();
        var artifacts = source.Where(a => a.Id != Guid.Empty && a.SessionId != Guid.Empty)
            .GroupBy(a => a.Id).Select(g => g.OrderByDescending(a => a.CreatedAt).First()).ToArray();
        var protectedIds = BuildProtectedSet(artifacts);
        foreach (var artifact in artifacts)
            if (now - artifact.CreatedAt < p.MinimumAge) protectedIds.Add(artifact.Id);
        var delete = new List<ContextArtifact>();
        foreach (var session in artifacts.GroupBy(a => a.SessionId))
        {
            var ordered = session.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToArray();
            var keep = ordered.Where(a => protectedIds.Contains(a.Id)).Select(a => a.Id).ToHashSet();
            foreach (var artifact in ordered)
            {
                if (keep.Contains(artifact.Id)) continue;
                if (now - artifact.CreatedAt < p.MinimumAge) continue;
                if (ordered.Length - delete.Count(d => d.SessionId == session.Key) > p.MaxArtifactsPerSession) delete.Add(artifact);
            }
            foreach (var artifact in ordered.Where(a => !keep.Contains(a.Id) && !delete.Contains(a) && now - a.CreatedAt >= p.MinimumAge))
            {
                var bytes = session.Where(a => !delete.Contains(a)).Sum(a => Math.Max(0, a.Bytes));
                if (bytes <= p.MaxBytesPerSession) break;
                delete.Add(artifact);
            }
        }
        return new ContextGcPlan(delete.Select(a => a.Id).ToArray(), delete.Sum(a => Math.Max(0, a.Bytes)), protectedIds.Count, "local-deterministic-retention");
    }

    private static HashSet<Guid> BuildProtectedSet(IReadOnlyCollection<ContextArtifact> artifacts)
    {
        var byId = artifacts.ToDictionary(a => a.Id);
        var keep = artifacts.Where(a => a.Pinned || a.IsCurrentHead).Select(a => a.Id).ToHashSet();
        foreach (var id in keep.ToArray())
        {
            var cursor = byId.TryGetValue(id, out var node) ? node.ParentId : null;
            while (cursor is Guid parent && keep.Add(parent)) cursor = byId.TryGetValue(parent, out var next) ? next.ParentId : null;
        }
        return keep;
    }
}
