using System.Security.Cryptography;
using System.Text;
using DragonKMG.Application;
using DragonKMG.ToolGateway;

namespace DragonKMG.Infrastructure;

public sealed class WorkspaceFileContextResolver : IWorkspaceFileContextResolver
{
    public async Task<WorkspaceFileContext> ReadAsync(string projectRoot, string path, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) throw new UnauthorizedAccessException();
            var root = Path.GetFullPath(projectRoot);
            var requested = Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(root, path));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var boundary = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!requested.StartsWith(boundary, comparison)) throw new UnauthorizedAccessException();
            var relative = Path.GetRelativePath(root, requested);
            var file = await new WorkspaceFileReader(root, 65536).ReadAsync(relative, ct);
            if (file.Bytes > 65536) throw new FileContextException("FileContextTooLarge", 400);
            var bytes = new UTF8Encoding(false, true).GetBytes(file.Content);
            return new WorkspaceFileContext(file.RelativePath, file.Content, file.Bytes, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (FileContextException) { throw; }
        catch (FileNotFoundException) { throw new FileContextException("FileUnavailable", 404); }
        catch (DirectoryNotFoundException) { throw new FileContextException("FileUnavailable", 404); }
        catch (DecoderFallbackException) { throw new FileContextException("FileAccessDenied", 403); }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("File exceeds ", StringComparison.Ordinal))
        { throw new FileContextException("FileContextTooLarge", 400); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or ArgumentException or IOException or InvalidOperationException or NotSupportedException)
        { throw new FileContextException("FileAccessDenied", 403); }
    }
}
