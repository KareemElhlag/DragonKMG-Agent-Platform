using DragonKMG.Domain;
using System.Text.Json;

namespace DragonKMG.Application;

public static class WorkflowValidator
{
    public static void Validate(WorkflowDefinition workflow, IReadOnlyList<WorkflowStage> stages, IReadOnlyList<WorkflowEdge> edges)
    {
        if (string.IsNullOrWhiteSpace(workflow.Name) || stages.Count == 0) throw new ArgumentException("A workflow needs a name and at least one stage.");
        if (stages.Any(s => s.WorkflowId != workflow.Id || string.IsNullOrWhiteSpace(s.Name) || s.AgentId == Guid.Empty) || stages.Select(s => s.Id).Distinct().Count() != stages.Count || stages.Select(s => s.Position).Distinct().Count() != stages.Count) throw new ArgumentException("Workflow stages must be unique, assigned, and belong to the workflow.");
        foreach (var stage in stages) { try { if (JsonSerializer.Deserialize<string[]>(stage.ChecklistJson) is null) throw new ArgumentException(); } catch (JsonException) { throw new ArgumentException($"Stage '{stage.Name}' has an invalid checklist."); } }
        var ids = stages.Select(s => s.Id).ToHashSet();
        if (edges.Any(e => e.WorkflowId != workflow.Id || !ids.Contains(e.FromStageId) || !ids.Contains(e.ToStageId) || e.FromStageId == e.ToStageId) || edges.Select(e => (e.FromStageId,e.ToStageId)).Distinct().Count() != edges.Count) throw new ArgumentException("Workflow edges must be unique, non-self-referential, and connect workflow stages.");
        var remaining = stages.ToDictionary(s => s.Id, _ => 0); foreach (var edge in edges) remaining[edge.ToStageId]++;
        var ready = new Queue<Guid>(remaining.Where(x => x.Value == 0).Select(x => x.Key)); var visited = 0;
        while (ready.TryDequeue(out var current)) { visited++; foreach (var edge in edges.Where(e => e.FromStageId == current)) if (--remaining[edge.ToStageId] == 0) ready.Enqueue(edge.ToStageId); }
        if (visited != stages.Count) throw new ArgumentException("Workflow must be acyclic.");
    }
}
