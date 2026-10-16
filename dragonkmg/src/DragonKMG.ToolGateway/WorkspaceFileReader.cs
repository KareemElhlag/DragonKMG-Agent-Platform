using System.Text;

namespace DragonKMG.ToolGateway;

public sealed class WorkspaceFileReader
{
    public const string ToolId = "workspace.file.read";
    public const int MaximumBytes = 1_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".yaml", ".yml", ".xml", ".cs", ".csproj", ".sln", ".ts", ".tsx", ".js", ".css", ".html", ".dart", ".py", ".ps1", ".sh", ".sql", ".csv"
    };

    private readonly string _workspaceRoot;
    private readonly int _maximumBytes;
    private static readonly HashSet<string> RestrictedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".ssh", ".aws", ".azure", ".codex", "vault"
    };

    public WorkspaceFileReader(string workspaceRoot, int maximumBytes = MaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (maximumBytes is < 1 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _maximumBytes = maximumBytes;
        _workspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        if (OperatingSystem.IsWindows() && _workspaceRoot.StartsWith("\\\\", StringComparison.Ordinal)) throw new UnauthorizedAccessException("Network and device workspace roots are not supported.");
        if (!Directory.Exists(_workspaceRoot)) throw new DirectoryNotFoundException("Workspace root does not exist.");
        ValidateAncestors(_workspaceRoot);
    }

    public async Task<WorkspaceFileReadResult> ReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':')) throw new UnauthorizedAccessException("Only a non-empty relative workspace path is allowed.");
        var fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var boundary = Path.EndsInDirectorySeparator(_workspaceRoot) ? _workspaceRoot : _workspaceRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(boundary, comparison) || string.Equals(fullPath, _workspaceRoot, comparison)) throw new UnauthorizedAccessException("Path escapes the workspace boundary.");
        ValidateFileName(Path.GetFileName(fullPath));
        var extension = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(extension)) throw new UnauthorizedAccessException($"File type '{extension}' is not allowed for read-only inspection.");
        ValidateAncestors(fullPath);
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > _maximumBytes) throw new InvalidOperationException($"File exceeds the {_maximumBytes} byte inspection limit.");
        // Check again after opening, and keep the read bounded even if the file grows.
        ValidateAncestors(fullPath);
        var bytes = new byte[_maximumBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken);
            if (read == 0) break;
            count += read;
        }
        if (count > _maximumBytes) throw new InvalidOperationException($"File exceeds the {_maximumBytes} byte inspection limit.");
        if (bytes.AsSpan(0, count).Contains((byte)0)) throw new InvalidOperationException("Binary file content is not allowed.");
        var content = new UTF8Encoding(false, true).GetString(bytes, 0, count);
        return new WorkspaceFileReadResult(Path.GetRelativePath(_workspaceRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/'), content, count);
    }

    private static void ValidateAncestors(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? throw new UnauthorizedAccessException("A filesystem root is required.");
        var current = root;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked workspace paths are not allowed.");
        foreach (var part in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = OperatingSystem.IsWindows() ? part.TrimEnd(' ', '.') : part;
            if (RestrictedDirectories.Contains(name)) throw new UnauthorizedAccessException("Private configuration directories cannot be inspected.");
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked workspace paths are not allowed.");
        }
    }

    private static void ValidateFileName(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(name);
        if (name.StartsWith(".env", StringComparison.Ordinal) ||
            Path.GetExtension(name) is ".pem" or ".key" or ".pfx" or ".p12" ||
            name is "credentials" or "credential" || stem is "credentials" or "credential" || name.StartsWith("credentials.", StringComparison.Ordinal) ||
            new[] { "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa" }.Any(key => name == key || name.StartsWith(key + ".", StringComparison.Ordinal)) ||
            (name.EndsWith(".json", StringComparison.Ordinal) && (name.StartsWith("secret", StringComparison.Ordinal) || name.StartsWith("appsettings", StringComparison.Ordinal))))
            throw new UnauthorizedAccessException("Secret and credential files cannot be inspected.");
    }
}

public sealed record WorkspaceFileReadResult(string RelativePath, string Content, int Bytes);
