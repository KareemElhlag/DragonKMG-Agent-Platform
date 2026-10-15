using DragonKMG.ToolGateway;

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
}
