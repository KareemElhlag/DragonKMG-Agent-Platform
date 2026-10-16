namespace DragonKMG.Domain;

// Organization metadata only: this record never grants filesystem or tool access.
public sealed record WorkspaceProject(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SessionHistoryPage(IReadOnlyList<SessionMessage> Messages, bool HasMore, string? NextCursor);
