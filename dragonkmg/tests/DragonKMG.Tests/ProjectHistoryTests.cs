using DragonKMG.Application;
using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class ProjectHistoryTests
{
    [Fact]
    public void NewProjectSelectionRequiresExistingFolderNotFileOrMissingPath()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dragon-project-folder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "sample.txt");
        File.WriteAllText(file, "synthetic test data");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var project = new WorkspaceProject(Guid.NewGuid(), "Folder", folder, now, now);
            ProjectPolicy.ValidateFolderSelection(project);
            Assert.Throws<ProjectFolderUnavailableException>(() => ProjectPolicy.ValidateFolderSelection(project with { RootPath = file }));
            Assert.Throws<ProjectFolderUnavailableException>(() => ProjectPolicy.ValidateFolderSelection(project with { RootPath = Path.Combine(folder, "missing") }));
            // Persistence metadata validation remains compatible with previously stored unavailable roots.
            ProjectPolicy.Validate(project with { RootPath = Path.Combine(folder, "missing") });
        }
        finally { File.Delete(file); Directory.Delete(folder); }
    }

    [Theory]
    [InlineData("//server/share")]
    [InlineData("//?/C:/Windows")]
    [InlineData("//./C:/Windows")]
    public void NewProjectSelectionRejectsNetworkAndDeviceNamespaces(string path)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new WorkspaceProject(Guid.NewGuid(), "Denied", path, now, now);
        Assert.Throws<ProjectFolderUnavailableException>(() => ProjectPolicy.ValidateFolderSelection(project));
    }

    [Fact]
    public async Task HistoryPagesOverOneHundredEqualTimestampsWithoutDuplicatesOrGaps()
    {
        using var fixture = await Fixture.CreateAsync();
        var timestamp = DateTimeOffset.UtcNow;
        var expected = new List<Guid>();
        for (var index = 0; index < 235; index++)
        {
            var message = new SessionMessage(Guid.NewGuid(), fixture.Session.Id, "user", $"message {index}", null, timestamp);
            expected.Add(message.Id);
            await fixture.Store.SaveSessionMessageAsync(message, default);
        }
        var other = fixture.Session with { Id = Guid.NewGuid(), Name = "other" };
        await fixture.Store.SaveTerminalSessionAsync(other, default);
        await fixture.Store.SaveSessionMessageAsync(new SessionMessage(Guid.NewGuid(), other.Id, "user", "isolated", null, timestamp), default);
        var actual = new List<Guid>();
        string? cursor = null;
        var pageCount = 0;
        do
        {
            var page = await fixture.Store.GetSessionHistoryAsync(fixture.Session.Id, cursor, 100, default);
            Assert.InRange(page.Messages.Count, 1, 100);
            Assert.All(page.Messages, message => Assert.Equal(fixture.Session.Id, message.SessionId));
            var keys = page.Messages.Select(message => message.Id.ToString()).ToArray();
            Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
            actual.AddRange(page.Messages.Select(message => message.Id));
            cursor = page.NextCursor;
            Assert.Equal(page.HasMore, cursor is not null);
            pageCount++;
        } while (cursor is not null);
        Assert.Equal(3, pageCount);
        Assert.Equal(235, actual.Distinct().Count());
        Assert.Equal(expected.Order(), actual.Order());
    }

    [Fact]
    public async Task ProjectMappingSurvivesStoreReopenAndDoesNotGrantTools()
    {
        using var fixture = await Fixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        // A deliberately nonexistent root is valid metadata, not a filesystem lookup or grant.
        var project = new WorkspaceProject(Guid.NewGuid(), "Workspace", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), now, now);
        await fixture.Store.SaveProjectAsync(project, default);
        await fixture.Store.SetSessionProjectAsync(fixture.Session.Id, project.Id, default);
        var reopened = new SqlitePlatformStore(fixture.Path);
        await reopened.InitializeAsync(default);
        Assert.Equal(project, Assert.Single(await reopened.ListProjectsAsync(default)));
        Assert.Equal(project.Id, await reopened.GetSessionProjectAsync(fixture.Session.Id, default));
        Assert.Single(await reopened.ListTerminalSessionsAsync(default));
        await reopened.SetSessionProjectAsync(fixture.Session.Id, null, default);
        Assert.Null(await reopened.GetSessionProjectAsync(fixture.Session.Id, default));
        Assert.Single(await reopened.ListProjectsAsync(default));
    }

    [Fact]
    public async Task InvalidMetadataAndMalformedCursorsFailClosed()
    {
        using var fixture = await Fixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var project = new WorkspaceProject(Guid.NewGuid(), "Workspace", "relative/path", now, now);
        Assert.Throws<ArgumentException>(() => ProjectPolicy.Validate(project));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.GetSessionHistoryAsync(fixture.Session.Id, "invalid!", 100, default));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.GetSessionHistoryAsync(fixture.Session.Id, new string('x', 257), 100, default));
        Assert.Empty((await fixture.Store.GetSessionHistoryAsync(fixture.Session.Id, null, 100, default)).Messages);
    }

    private sealed class Fixture(string path, SqlitePlatformStore store, TerminalSession session) : IDisposable
    {
        public string Path => path;
        public SqlitePlatformStore Store => store;
        public TerminalSession Session => session;
        public static async Task<Fixture> CreateAsync()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon-history-{Guid.NewGuid():N}.db");
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(default);
            var now = DateTimeOffset.UtcNow;
            var session = new TerminalSession(Guid.NewGuid(), "history-test", null, now, now);
            await store.SaveTerminalSessionAsync(session, default);
            return new Fixture(path, store, session);
        }
        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
