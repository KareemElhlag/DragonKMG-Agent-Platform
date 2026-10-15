using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class DefaultSkillTests
{
    [Fact]
    public async Task InitializationSeedsTheFourDefaultSkillsAsync()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"dragonkmg-{Guid.NewGuid():N}.db");
        var store = new SqlitePlatformStore(path);

        try
        {
            // Act
            await store.InitializeAsync(CancellationToken.None);
            var skills = await store.ListSkillsAsync(CancellationToken.None);

            // Assert
            Assert.Contains(skills, x => x.Key == "kmg-engineering-standard" && x.SourceRef == "kmg-agent-skill/SKILL.md");
            Assert.Contains(skills, x => x.Key == "cyber-kmg-security" && x.SourceRef == "cyber-kmg-security/SKILL.md");
            Assert.Contains(skills, x => x.Key == "nexusecore-ui-ux" && x.SourceRef == "nexusecore-ui-ux/SKILL.md");
            Assert.Contains(skills, x => x.Key == "flutter-architect-review" && x.SourceRef == "flutter-architect-review/SKILL.md");
            Assert.All(skills, skill => Assert.Equal("project-file", skill.SourceType));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" }) File.Delete(path + suffix);
        }
    }
}
