using System.Net.Http.Json;
using DragonKMG.Domain;

namespace DragonKMG.ToolGateway;

public sealed class N8nWebhookConnector(HttpClient client, ApprovalGate approvals, IReadOnlySet<string> allowedHosts)
{
    public async Task InvokeAsync(Uri webhook, object payload, string idempotencyKey, ApprovalRequest? approval, CancellationToken cancellationToken)
    {
        if (webhook.Scheme != Uri.UriSchemeHttps || !allowedHosts.Contains(webhook.Host)) throw new UnauthorizedAccessException("Webhook host is not allowlisted HTTPS.");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        var decision = approvals.Authorize(approval, DateTimeOffset.UtcNow);
        if (!decision.Allowed) throw new UnauthorizedAccessException(decision.Reason);
        using var request = new HttpRequestMessage(HttpMethod.Post, webhook) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
