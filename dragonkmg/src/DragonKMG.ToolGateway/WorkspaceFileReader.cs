using System.Text;

namespace DragonKMG.ToolGateway;

public sealed class WorkspaceFileReader
{
    public const string ToolId = "workspace.file.read";
    private const long MaximumBytes = 1_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".yaml", ".yml", ".xml", ".cs", ".csproj", ".sln", ".ts", ".tsx", ".js", ".css", ".html", ".dart", ".py", ".ps1", ".sh", ".sql", ".csv"
    };

    private readonly string _workspaceRoot;

    public WorkspaceFileReader(string workspaceRoot)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(_workspaceRoot)) throw new DirectoryNotFoundException("Workspace root does not exist.");
    }

    public async Task<WorkspaceFileReadResult> ReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) throw new UnauthorizedAccessException("Only a non-empty relative workspace path is allowed.");
        var fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
        if (!fullPath.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Path escapes the workspace boundary.");
        var extension = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(extension)) throw new UnauthorizedAccessException($"File type '{extension}' is not allowed for read-only inspection.");
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Workspace file was not found.", relativePath);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Symbolic links and reparse points are not allowed.");
        if (info.Length > MaximumBytes) throw new InvalidOperationException($"File exceeds the {MaximumBytes} byte inspection limit.");
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        if (bytes.Contains((byte)0)) throw new InvalidOperationException("Binary file content is not allowed.");
        return new WorkspaceFileReadResult(relativePath.Replace('\\', '/'), Encoding.UTF8.GetString(bytes), bytes.Length);
    }
}

public sealed record WorkspaceFileReadResult(string RelativePath, string Content, int Bytes);
