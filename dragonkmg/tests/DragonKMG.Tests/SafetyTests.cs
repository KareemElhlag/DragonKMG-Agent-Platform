using DragonKMG.ToolGateway;

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
}
