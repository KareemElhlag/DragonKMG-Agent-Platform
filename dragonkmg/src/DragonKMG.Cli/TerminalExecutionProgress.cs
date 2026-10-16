using DragonKMG.Application;

namespace DragonKMG.Cli;

internal sealed class TerminalExecutionProgress(TerminalSurface surface) : IProgress<SessionExecutionProgress>
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<SessionExecutionProgress> _pending = new();
    public void Report(SessionExecutionProgress value) => _pending.Enqueue(value);
    public void Flush() { while (_pending.TryDequeue(out var value)) surface.Add("process", Format(value)); }
    internal static string Format(SessionExecutionProgress value) => value.Phase switch
    {
        "validating_files" => "CONTEXT · validating requested file context; no automatic file access",
        "running_agent" => $"RUNNING · {value.AgentName} · {value.ModelId} · member {value.Member}/{value.Total}",
        "agent_completed" => $"FINISHED · {value.AgentName} · member {value.Member}/{value.Total}",
        "completed" => "SAVED · team response persisted",
        "cancelled" => "CANCELLED · remaining members not started",
        "failed" => "FAILED · remaining members not started",
        _ => "STATUS · awaiting execution update"
    };
}
