using System.Collections.Concurrent;

namespace DragonKMG.Application;

public sealed record SessionExecutionProgress(string Phase, string? AgentName = null, string? ModelId = null, int Member = 0, int Total = 0);
public sealed record SessionExecutionSnapshot(Guid RequestId, DateTimeOffset UpdatedAt, IReadOnlyList<SessionExecutionProgress> Events);

// Transient operational status only. Durable messages/runs remain in IPlatformStore.
public sealed class SessionExecutionTracker
{
    private readonly ConcurrentDictionary<Guid, SessionExecutionSnapshot> _sessions = new();
    public IProgress<SessionExecutionProgress> Observe(Guid sessionId, Guid requestId)
    {
        IProgress<SessionExecutionProgress>? active = null;
        return new Reporter(value => { active ??= Start(sessionId, requestId); active.Report(value); });
    }
    public IProgress<SessionExecutionProgress> Start(Guid sessionId, Guid requestId)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _sessions.Where(pair => pair.Value.UpdatedAt < now.AddMinutes(-10))) _sessions.TryRemove(entry.Key, out _);
        if (_sessions.Count >= 1024) foreach (var entry in _sessions.OrderBy(pair => pair.Value.UpdatedAt).Take(_sessions.Count - 1023)) _sessions.TryRemove(entry.Key, out _);
        _sessions[sessionId] = new(requestId, now, []);
        return new Reporter(value => _sessions.AddOrUpdate(sessionId,
            _ => new(requestId, DateTimeOffset.UtcNow, [value]),
            (_, old) => old.RequestId != requestId ? old : new(requestId, DateTimeOffset.UtcNow, old.Events.Append(value).TakeLast(32).ToArray())));
    }
    public SessionExecutionSnapshot? Get(Guid sessionId) => _sessions.TryGetValue(sessionId, out var value) && value.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-10) ? value : null;
    private sealed class Reporter(Action<SessionExecutionProgress> report) : IProgress<SessionExecutionProgress>
    {
        public void Report(SessionExecutionProgress value) => report(value);
    }
}
