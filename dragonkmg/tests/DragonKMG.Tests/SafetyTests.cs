using DragonKMG.ToolGateway;
using DragonKMG.Domain;

namespace DragonKMG.Tests;

public sealed class SafetyTests
{
    [Fact]
    public void ProcessExecutionWithoutSandboxIsDenied()
    {
        // Arrange
        var policy = new ToolPolicy();

        // Act
        var decision = policy.Authorize(ToolRiskClass.ProcessExecution, explicitlyGranted: true, sandboxAvailable: false);

        // Assert
        Assert.False(decision.Allowed);
        Assert.Contains("sandbox", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DestructiveToolsAreDeniedEvenWhenGranted()
    {
        // Arrange
        var policy = new ToolPolicy();

        // Act
        var decision = policy.Authorize(ToolRiskClass.Destructive, explicitlyGranted: true, sandboxAvailable: true);

        // Assert
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void SideEffectApprovalMustBeApprovedAndUnexpired()
    {
        var gate = new ApprovalGate();
        var now = DateTimeOffset.UtcNow;
        var pending = new ApprovalRequest(Guid.NewGuid(), null, "webhook", "test", ApprovalStatus.Pending, now, now.AddMinutes(5), null);
        var approved = pending with { Status = ApprovalStatus.Approved, ResolvedAt = now };

        Assert.False(gate.Authorize(pending, now).Allowed);
        Assert.True(gate.Authorize(approved, now).Allowed);
        Assert.False(gate.Authorize(approved, now.AddMinutes(6)).Allowed);
    }
}
