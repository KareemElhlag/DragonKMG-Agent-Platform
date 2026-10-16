using DragonKMG.Application;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class WorkspaceFileContextResolverTests
{
    [Fact]
    public async Task RelativeAndAbsoluteInsideProjectResolveWithOriginalByteHash()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragon-file-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "example file.md");
        var bytes = System.Text.Encoding.UTF8.GetBytes("مرحبا synthetic\n");
        await File.WriteAllBytesAsync(filePath, bytes);
        try
        {
            var resolver = new WorkspaceFileContextResolver();
            var relative = await resolver.ReadAsync(root, "example file.md", default);
            var absolute = await resolver.ReadAsync(root, filePath, default);
            Assert.Equal(relative, absolute);
            Assert.Equal(bytes.Length, relative.Bytes);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), relative.Sha256);
            Assert.Equal("example file.md", relative.RelativePath);
            var denied = await Assert.ThrowsAsync<FileContextException>(() => resolver.ReadAsync(root, Path.Combine(Path.GetTempPath(), "outside.md"), default));
            Assert.Equal("FileAccessDenied", denied.Code);
            Assert.DoesNotContain(root, denied.Message);
        }
        finally { File.Delete(filePath); Directory.Delete(root); }
    }

    [Fact]
    public async Task OversizedAndSecretFilesAreRejectedWithSafeCodes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragon-file-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var oversized = Path.Combine(root, "large.md");
        var secret = Path.Combine(root, ".env.json");
        await File.WriteAllTextAsync(oversized, new string('a', 65537));
        await File.WriteAllTextAsync(secret, "synthetic");
        try
        {
            var resolver = new WorkspaceFileContextResolver();
            var tooLarge = await Assert.ThrowsAsync<FileContextException>(() => resolver.ReadAsync(root, oversized, default));
            Assert.Equal("FileContextTooLarge", tooLarge.Code);
            var denied = await Assert.ThrowsAsync<FileContextException>(() => resolver.ReadAsync(root, secret, default));
            Assert.Equal("FileAccessDenied", denied.Code);
            var missing = await Assert.ThrowsAsync<FileContextException>(() => resolver.ReadAsync(root, "missing.md", default));
            Assert.Equal("FileUnavailable", missing.Code);
        }
        finally { File.Delete(oversized); File.Delete(secret); Directory.Delete(root); }
    }
}
