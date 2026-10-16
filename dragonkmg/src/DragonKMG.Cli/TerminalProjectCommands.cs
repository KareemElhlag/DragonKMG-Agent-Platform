using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Cli;

internal static class TerminalProjectCommands
{
    public static async Task<string> ExecuteAsync(IPlatformStore store, Guid sessionId, string argument, CancellationToken ct)
    {
        if (argument.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            await BindAsync(store, sessionId, null, ct);
            return "Project cleared. File requests require a project root.";
        }
        if (argument.Equals("cwd", StringComparison.OrdinalIgnoreCase))
        {
            var root = Directory.GetCurrentDirectory();
            var project = (await store.ListProjectsAsync(ct)).FirstOrDefault(value => value.RootPath.Equals(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (project is null)
            {
                var now = DateTimeOffset.UtcNow;
                project = new WorkspaceProject(Guid.NewGuid(), new DirectoryInfo(root).Name, root, now, now);
                await store.SaveProjectAsync(project, ct);
            }
            await BindAsync(store, sessionId, project.Id, ct);
            return "Project selected: " + project.Name + "\n" + project.RootPath + "\nNo tool grant was added.";
        }
        if (argument.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
        {
            var selector = argument[4..].Trim();
            var matches = (await store.ListProjectsAsync(ct)).Where(value => value.Id.ToString().Equals(selector, StringComparison.OrdinalIgnoreCase) || value.Name.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) return "Project missing or name ambiguous. Use the full project ID from /projects.";
            await BindAsync(store, sessionId, matches[0].Id, ct);
            return "Project selected: " + matches[0].Name + "\n" + matches[0].RootPath;
        }
        return "/projects · /project cwd · /project use <name-or-id> · /project clear";
    }

    private static async Task BindAsync(IPlatformStore store, Guid sessionId, Guid? projectId, CancellationToken ct)
    {
        var owner = Guid.NewGuid();
        if (!await store.TryAcquireSessionLeaseAsync(sessionId, owner, DateTimeOffset.UtcNow.AddMinutes(1), ct)) throw new SessionBusyException();
        try { await store.SetSessionProjectAsync(sessionId, projectId, ct); }
        finally { await store.ReleaseSessionLeaseAsync(sessionId, owner, CancellationToken.None); }
    }
}
