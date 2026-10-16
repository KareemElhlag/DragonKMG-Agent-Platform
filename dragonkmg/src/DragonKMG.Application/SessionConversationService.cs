using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class SessionBusyException : Exception;
public sealed partial class SessionConversationService(IPlatformStore store, PlatformService platform, IWorkspaceFileContextResolver? fileContexts = null)
{

    public async Task<AgentRun> SendAsync(Guid sessionId, Guid agentId, string input, CancellationToken ct, bool quickChat = false)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 16000) throw new ArgumentException("Input must contain 1–16000 characters.");
        var session = (await store.ListTerminalSessionsAsync(ct)).FirstOrDefault(value => value.Id == sessionId)
            ?? throw new KeyNotFoundException("Session unavailable.");
        var owner = Guid.NewGuid();
        if (!await store.TryAcquireSessionLeaseAsync(sessionId, owner, DateTimeOffset.UtcNow.AddMinutes(3), ct)) throw new SessionBusyException();
        try
        {
            var agent = await store.FindAgentAsync(agentId, ct) ?? throw new KeyNotFoundException("Agent unavailable.");
            if (!agent.Enabled) throw new ArgumentException("Agent disabled.");
            await store.SaveTerminalSessionAsync(session with { SelectedAgentId = agentId, LastOpenedAt = DateTimeOffset.UtcNow }, ct);
            await store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), sessionId, "user", input.Trim(), null, DateTimeOffset.UtcNow), ct);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                var run = await platform.RunAgentAsync(agentId, input.Trim(), timeout.Token, quickChat, allowLocalGreeting: true);
                await store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), sessionId, "assistant", run.Output ?? "", run.Id, DateTimeOffset.UtcNow), CancellationToken.None);
                return run;
            }
            catch (Exception exception)
            {
                var code = exception is OperationCanceledException ? "Request cancelled." : "Request failed. Check the connection and agent settings before retrying.";
                await store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), sessionId, "system", code, null, DateTimeOffset.UtcNow), CancellationToken.None);
                throw;
            }
        }
        finally { await store.ReleaseSessionLeaseAsync(sessionId, owner, CancellationToken.None); }
    }
}
