using Microsoft.Agents.AI;

namespace DragonKMG.FrameworkAdapter;

/// <summary>Identifies the verified Microsoft Agent Framework boundary used by DragonKMG.</summary>
public sealed class AgentFrameworkAdapter
{
    public string FrameworkAssemblyName => typeof(AIAgent).Assembly.GetName().Name ?? "Microsoft.Agents.AI";
}
