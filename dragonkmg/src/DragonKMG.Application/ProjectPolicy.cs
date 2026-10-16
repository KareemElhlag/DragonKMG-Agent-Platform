using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class ProjectFolderUnavailableException : ArgumentException
{
    public ProjectFolderUnavailableException() : base("Select an existing local project folder.") { }
}

public static class ProjectPolicy
{
    public static void ValidateFolderSelection(WorkspaceProject project)
    {
        Validate(project);
        // Reject network/device namespaces before checking existence; no network probes or enumeration.
        var windowsPath = project.RootPath.Replace('/', '\\');
        if (windowsPath.StartsWith("\\\\", StringComparison.Ordinal) || windowsPath.StartsWith("\\??\\", StringComparison.Ordinal))
            throw new ProjectFolderUnavailableException();
        if (!Directory.Exists(project.RootPath)) throw new ProjectFolderUnavailableException();
    }

    public static void Validate(WorkspaceProject project)
    {
        if (project.Id == Guid.Empty || string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > 100 || project.Name != project.Name.Trim() || project.Name.Any(char.IsControl) || string.IsNullOrWhiteSpace(project.RootPath) || project.RootPath.Length > 4096 || project.RootPath.Any(char.IsControl) || !Path.IsPathFullyQualified(project.RootPath) || project.CreatedAt == default || project.UpdatedAt < project.CreatedAt)
            throw new ArgumentException("Invalid project metadata.");
        // Do not check directory existence, enumerate contents, resolve links, or issue access grants.
    }
}
