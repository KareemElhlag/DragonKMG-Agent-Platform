using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Tests;

public sealed class WorkflowValidatorTests
{
    [Fact]
    public void AcceptsAcyclicGraphAndRejectsCycle()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "release", "release workflow", true, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var first = new WorkflowStage(Guid.NewGuid(), workflow.Id, "implement", Guid.NewGuid(), 1, "[\"implement\"]", "implementation evidence", false);
        var second = new WorkflowStage(Guid.NewGuid(), workflow.Id, "review", Guid.NewGuid(), 2, "[\"review\"]", "review evidence", false);
        var edge = new WorkflowEdge(workflow.Id, first.Id, second.Id);

        WorkflowValidator.Validate(workflow, [first, second], [edge]);
        Assert.Throws<ArgumentException>(() => WorkflowValidator.Validate(workflow, [first, second], [edge, new WorkflowEdge(workflow.Id, second.Id, first.Id)]));
    }
}
