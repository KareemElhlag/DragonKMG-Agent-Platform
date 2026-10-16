namespace DragonKMG.Domain;

public sealed record TeamDefinition(Guid Id, string Name, string Description, bool Enabled, IReadOnlyList<Guid> AgentIds, int Version);
