using DragonKMG.Application;

namespace DragonKMG.Tests;

public sealed class ContextGarbageCollectorTests
{
    [Fact]
    public void Keeps_current_head_and_its_parent_chain()
    {
        var session = Guid.NewGuid();
        var old = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow.AddDays(-10), Bytes: 10);
        var middle = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Checkpoint, DateTimeOffset.UtcNow.AddDays(-5), old.Id, Bytes: 20);
        var head = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Checkpoint, DateTimeOffset.UtcNow.AddDays(-1), middle.Id, IsCurrentHead: true, Bytes: 30);
        var stale = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow.AddDays(-8), Bytes: 50);
        var plan = new ContextGarbageCollector().Plan([old, middle, head, stale], DateTimeOffset.UtcNow, new(TimeSpan.Zero, 3, 1));
        Assert.DoesNotContain(old.Id, plan.DeleteIds);
        Assert.DoesNotContain(middle.Id, plan.DeleteIds);
        Assert.DoesNotContain(head.Id, plan.DeleteIds);
        Assert.Contains(stale.Id, plan.DeleteIds);
        Assert.Equal(50, plan.ReclaimedBytes);
    }

    [Fact]
    public void Grace_period_and_pinned_nodes_are_never_reclaimed()
    {
        var session = Guid.NewGuid();
        var fresh = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow, Bytes: 100);
        var pinned = new ContextArtifact(Guid.NewGuid(), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow.AddDays(-100), Pinned: true, Bytes: 100);
        var plan = new ContextGarbageCollector().Plan([fresh, pinned], DateTimeOffset.UtcNow, new(TimeSpan.FromHours(1), 1, 1));
        Assert.Empty(plan.DeleteIds);
        Assert.Equal(2, plan.ProtectedCount);
    }

    [Fact]
    public void Planner_is_deterministic_and_has_no_provider_dependency()
    {
        var session = Guid.NewGuid();
        var a = new ContextArtifact(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow.AddDays(-2), Bytes: 10);
        var b = new ContextArtifact(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), session, ContextArtifactKind.Summary, DateTimeOffset.UtcNow.AddDays(-3), Bytes: 20);
        var collector = new ContextGarbageCollector();
        var first = collector.Plan([a, b], DateTimeOffset.UtcNow, new(TimeSpan.Zero, 1, long.MaxValue));
        var second = collector.Plan([b, a], DateTimeOffset.UtcNow, new(TimeSpan.Zero, 1, long.MaxValue));
        Assert.Equal(first.DeleteIds, second.DeleteIds);
        Assert.Single(first.DeleteIds);
    }
}
