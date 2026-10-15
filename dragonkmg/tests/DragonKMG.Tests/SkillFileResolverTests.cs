using DragonKMG.Domain;
using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class SkillFileResolverTests
{
    private static SkillDefinition FileSkill(string path) => new(Guid.NewGuid(), "test", "Test", "Test", 1, true, SkillType.Instruction, "", "[]", "local", "project-file", path);

    [Fact]
    public async Task ConcurrentSkillUpdatesCannotOverwriteTheSameVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragon-skill-version-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(default);
            var original = (await store.ListSkillsAsync(default))[0];
            var first = original with { Name = "First", Version = original.Version + 1 };
            var second = original with { Name = "Second", Version = original.Version + 1 };
            var results = await Task.WhenAll(store.TrySaveSkillVersionAsync(first, default), store.TrySaveSkillVersionAsync(second, default));
            Assert.Single(results.Where(saved => saved));
            Assert.Equal(original.Version + 1, (await store.FindSkillAsync(original.Id, default))!.Version);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task ReloadsCompleteFileOnEveryResolutionAndRejectsUnsafeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragon-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "SKILL.md");
            var resolver = new SkillFileResolver(root);
            await File.WriteAllTextAsync(path, "# Full skill\nFirst instruction\nSecond instruction");
            Assert.Contains("Second instruction", await resolver.ResolveAsync(FileSkill("SKILL.md"), default));
            await File.WriteAllTextAsync(path, "# Updated skill\nImproved instructions");
            Assert.Contains("Improved instructions", await resolver.ResolveAsync(FileSkill("SKILL.md"), default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => resolver.ResolveAsync(FileSkill("../outside.md"), default));
            await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync(FileSkill("secrets.json"), default));
            await File.WriteAllTextAsync(path, new string('x', SkillFileResolver.MaximumBytes + 1));
            await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync(FileSkill("SKILL.md"), default));
            await File.WriteAllBytesAsync(path, [0xff, 0xfe, 0xff]);
            await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(() => resolver.ResolveAsync(FileSkill("SKILL.md"), default));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InlineContentAndSeededSkillEditsSurviveInitialization()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragon-skills-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqlitePlatformStore(path);
            await store.InitializeAsync(default);
            var seeded = (await store.ListSkillsAsync(default))[0];
            var changed = seeded with { SourceType = "inline", InstructionContent = "My full custom skill", Enabled = false, Version = 3 };
            await store.SaveSkillAsync(changed, default);
            await store.InitializeAsync(default);
            Assert.Equal(changed, await store.FindSkillAsync(changed.Id, default));
            Assert.Equal("My full custom skill", await new SkillFileResolver(Path.GetTempPath()).ResolveAsync(changed, default));
            var defaultSkill = (await store.ListSkillsAsync(default)).First(skill => skill.SourceType == "project-file");
            var content = await new SkillFileResolver(Path.Combine(AppContext.BaseDirectory, "skills")).ResolveAsync(defaultSkill, default);
            Assert.StartsWith("---", content);
            Assert.True(content.Length > 1000);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
