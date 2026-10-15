namespace DragonKMG.ToolGateway;

public enum ToolRiskClass { ReadOnly, Write, NetworkEgress, ProcessExecution, Destructive }
public sealed record ToolDecision(bool Allowed, bool RequiresApproval, string Reason);

public sealed class ToolPolicy
{
    public ToolDecision Authorize(ToolRiskClass riskClass, bool explicitlyGranted, bool sandboxAvailable) => riskClass switch
    {
        ToolRiskClass.ReadOnly when explicitlyGranted => new(true, false, "Granted read-only tool."),
        ToolRiskClass.Write or ToolRiskClass.NetworkEgress when explicitlyGranted => new(true, true, "Explicit approval is required before this side effect."),
        ToolRiskClass.ProcessExecution when explicitlyGranted && sandboxAvailable => new(true, true, "Sandboxed process execution requires approval."),
        ToolRiskClass.ProcessExecution => new(false, false, "Process execution is disabled without an approved isolated sandbox."),
        ToolRiskClass.Destructive => new(false, false, "Destructive tools are disabled by default."),
        _ => new(false, false, "Tool is not granted to this agent.")
    };
}
