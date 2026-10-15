using DragonKMG.Domain;

namespace DragonKMG.ToolGateway;

public sealed record ApprovalDecision(bool Allowed, string Reason);

public sealed class ApprovalGate
{
    public ApprovalDecision Authorize(ApprovalRequest? approval, DateTimeOffset now) => approval switch
    {
        null => new(false, "A durable approval is required before this side effect."),
        { Status: ApprovalStatus.Approved, ExpiresAt: var expiresAt } when expiresAt > now => new(true, "Approval is active."),
        { Status: ApprovalStatus.Approved } => new(false, "Approval expired before execution."),
        { Status: ApprovalStatus.Pending } => new(false, "Approval is still pending."),
        { Status: ApprovalStatus.Rejected } => new(false, "Approval was rejected."),
        _ => new(false, "Approval is expired or invalid.")
    };
}
