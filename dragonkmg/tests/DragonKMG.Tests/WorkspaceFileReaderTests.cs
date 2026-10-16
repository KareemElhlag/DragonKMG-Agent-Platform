using DragonKMG.ToolGateway;
using System.Text;

namespace DragonKMG.Tests;

public sealed class WorkspaceFileReaderTests
{
    [Fact]
    public async Task ReadsAllowedTextOnlyInsideWorkspaceAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragonkmg-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "verified workspace content");
            var reader = new WorkspaceFileReader(root);
            var result = await reader.ReadAsync("notes.md", CancellationToken.None);
            Assert.Equal("verified workspace content", result.Content);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync("../outside.md", CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync("notes.exe", CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("../workspace-sibling/notes.md")]
    [InlineData("nested/../../outside.md")]
    [InlineData("notes.md:stream.md")]
    public async Task RejectsEscapesSiblingPrefixesAndAlternateStreams(string path)
    {
        using var fixture = new WorkspaceFixture();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceFileReader(fixture.Root).ReadAsync(path, default));
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.production.json")]
    [InlineData("private.pem")]
    [InlineData("private.key")]
    [InlineData("private.pfx")]
    [InlineData("private.p12")]
    [InlineData("id_rsa.txt")]
    [InlineData("id_ed25519.json")]
    [InlineData("credentials.json")]
    [InlineData("credentials.backup.json")]
    [InlineData("credential.yaml")]
    [InlineData("secrets.local.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData(".git/config.json")]
    [InlineData(".ssh/config.json")]
    [InlineData(".aws/config.json")]
    [InlineData(".azure/config.json")]
    [InlineData(".codex/config.json")]
    [InlineData("vault/readme.md")]
    public async Task RejectsPrivateConfigurationEvenWhenTextExtensionIsAllowed(string path)
    {
        using var fixture = new WorkspaceFixture();
        var fullPath = Path.Combine(fixture.Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "synthetic credential fixture, no real secrets");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceFileReader(fixture.Root).ReadAsync(path, default));
    }

    [Fact]
    public async Task PreservesUtf8AndBomAndNormalizesRelativeResult()
    {
        using var fixture = new WorkspaceFixture();
        var content = "\uFEFF# مشروع\nمرحبا 🐉";
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root, "README.md"), Encoding.UTF8.GetBytes(content));
        var result = await new WorkspaceFileReader(fixture.Root).ReadAsync("./README.md", default);
        Assert.Equal("README.md", result.RelativePath);
        Assert.Equal(content, result.Content);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), result.Bytes);
    }

    [Fact]
    public async Task RejectsMalformedUtf8AndNullBinary()
    {
        using var fixture = new WorkspaceFixture();
        var file = Path.Combine(fixture.Root, "notes.md");
        var reader = new WorkspaceFileReader(fixture.Root);
        await File.WriteAllBytesAsync(file, [0xc3, 0x28]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => reader.ReadAsync("notes.md", default));
        await File.WriteAllBytesAsync(file, [0x61, 0x00, 0x62]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync("notes.md", default));
    }

    [Fact]
    public async Task EnforcesGlobalAndSmallerCallerLimits()
    {
        using var fixture = new WorkspaceFixture();
        var file = Path.Combine(fixture.Root, "notes.md");
        await File.WriteAllTextAsync(file, new string('x', WorkspaceFileReader.MaximumBytes));
        Assert.Equal(WorkspaceFileReader.MaximumBytes, (await new WorkspaceFileReader(fixture.Root).ReadAsync("notes.md", default)).Bytes);
        await File.WriteAllTextAsync(file, new string('x', WorkspaceFileReader.MaximumBytes + 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkspaceFileReader(fixture.Root).ReadAsync("notes.md", default));
        await File.WriteAllTextAsync(file, "12345");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkspaceFileReader(fixture.Root, 4).ReadAsync("notes.md", default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceFileReader(fixture.Root, WorkspaceFileReader.MaximumBytes + 1));
    }

    [Fact]
    public async Task FilesystemRootBoundaryDoesNotRequireDoubleSeparator()
    {
        using var fixture = new WorkspaceFixture();
        var file = Path.Combine(fixture.Root, "README.md");
        await File.WriteAllTextAsync(file, "root boundary fixture");
        var root = Path.GetPathRoot(fixture.Root)!;
        var result = await new WorkspaceFileReader(root).ReadAsync(Path.GetRelativePath(root, file), default);
        Assert.Equal("root boundary fixture", result.Content);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceFileReader(fixture.Root).ReadAsync(file, default));
    }

    [PosixCaseFact]
    public async Task DifferentCaseSiblingCannotEscapeCaseSensitiveRoot()
    {
        using var fixture = new WorkspaceFixture();
        var sibling = Path.Combine(fixture.Container, "WORKSPACE");
        Directory.CreateDirectory(sibling);
        await File.WriteAllTextAsync(Path.Combine(sibling, "notes.md"), "outside fixture");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceFileReader(fixture.Root).ReadAsync("../WORKSPACE/notes.md", default));
    }

    [SymlinkFact]
    public async Task RejectsLinkedFileDirectoryRootAndRootAncestor()
    {
        using var fixture = new WorkspaceFixture();
        var outside = Path.Combine(fixture.Container, "outside");
        var nested = Path.Combine(outside, "nested");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "notes.md");
        await File.WriteAllTextAsync(file, "outside fixture");
        var childLink = Path.Combine(fixture.Root, "linked");
        var fileLink = Path.Combine(fixture.Root, "notes.md");
        var parentLink = Path.Combine(fixture.Container, "parent-link");
        Directory.CreateSymbolicLink(childLink, nested);
        File.CreateSymbolicLink(fileLink, file);
        Directory.CreateSymbolicLink(parentLink, outside);
        try
        {
            var reader = new WorkspaceFileReader(fixture.Root);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync("linked/notes.md", default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync("notes.md", default));
            Assert.Throws<UnauthorizedAccessException>(() => new WorkspaceFileReader(childLink));
            Assert.Throws<UnauthorizedAccessException>(() => new WorkspaceFileReader(Path.Combine(parentLink, "nested")));
        }
        finally
        {
            File.Delete(fileLink);
            Directory.Delete(childLink);
            Directory.Delete(parentLink);
        }
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        public string Container { get; } = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), $"dragon-workspace-{Guid.NewGuid():N}");
        public string Root => Path.Combine(Container, "workspace");
        public WorkspaceFixture() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Container, true);
    }

    public sealed class PosixCaseFactAttribute : FactAttribute
    {
        public PosixCaseFactAttribute()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) Skip = "Case-sensitive filesystem sibling behavior is exercised on Linux; Windows/macOS default filesystems are case-insensitive.";
        }
    }

    public sealed class SymlinkFactAttribute : FactAttribute
    {
        public SymlinkFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) return;
            var root = Path.Combine(Path.GetTempPath(), $"dragon-link-capability-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var link = Path.Combine(root, "link");
            try { Directory.CreateSymbolicLink(link, root); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                Skip = "Windows symlink creation unavailable: Developer Mode or symbolic-link privilege is required. This case is skipped, not verified.";
            }
            finally
            {
                if (Directory.Exists(link)) Directory.Delete(link);
                Directory.Delete(root);
            }
        }
    }
}
