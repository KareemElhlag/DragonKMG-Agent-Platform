using DragonKMG.ToolGateway;
using DragonKMG.Domain;
using System.Net;

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

    [Fact]
    public async Task N8nWebhookRequiresAllowlistApprovalAndIdempotencyAsync()
    {
        using var client = new HttpClient(new CaptureHandler());
        var connector = new N8nWebhookConnector(client, new ApprovalGate(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "automation.example" });
        var now = DateTimeOffset.UtcNow;
        var approval = new ApprovalRequest(Guid.NewGuid(), null, "n8n", "test", ApprovalStatus.Approved, now, now.AddMinutes(1), now);
        await connector.InvokeAsync(new Uri("https://automation.example/webhook"), new { ok = true }, "test-idempotency", approval, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => connector.InvokeAsync(new Uri("https://other.example/webhook"), new { ok = true }, "key", approval, CancellationToken.None));
    }

    [Fact]
    public void McpDescriptorRequiresTrustedServerGrantAndSchema()
    {
        var policy = new McpTrustPolicy(new HashSet<string>(StringComparer.Ordinal) { "mock-server" });
        Assert.Throws<UnauthorizedAccessException>(() => policy.AuthorizeDescriptor("unknown", "read", "{}", true));
        Assert.Throws<UnauthorizedAccessException>(() => policy.AuthorizeDescriptor("mock-server", "read", "{}", false));
        var descriptor = policy.AuthorizeDescriptor("mock-server", "read", "{\"type\":\"object\"}", true);
        Assert.True(descriptor.Trusted);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.True(request.Headers.Contains("Idempotency-Key"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
