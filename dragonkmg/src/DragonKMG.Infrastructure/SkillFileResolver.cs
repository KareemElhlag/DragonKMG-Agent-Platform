using System.Text;
using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Infrastructure;

public sealed class SkillFileResolver(string rootPath) : ISkillContentResolver
{
    public const int MaximumBytes = 256 * 1024;
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task<string> ResolveAsync(SkillDefinition skill, CancellationToken cancellationToken)
    {
        if (skill.SourceType != "project-file") return skill.InstructionContent;
        if (string.IsNullOrWhiteSpace(skill.SourceRef)) throw new ArgumentException("Skill file path is required.");
        var file = Path.GetFullPath(Path.Combine(_root, skill.SourceRef));
        var relative = Path.GetRelativePath(_root, file);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Skill path must remain within the configured skills directory.");
        if (!string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Skill instruction files must use the .md extension.");
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked skill roots are not allowed.");
        var current = _root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked skill paths are not allowed.");
        }
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (stream.Length > MaximumBytes) throw new ArgumentException("Skill file exceeds 256 KiB.");
        var bytes = new byte[MaximumBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken);
            if (read == 0) break;
            count += read;
        }
        if (count > MaximumBytes) throw new ArgumentException("Skill file exceeds 256 KiB.");
        var content = new UTF8Encoding(false, true).GetString(bytes, 0, count).TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(content) || content.Contains('\0')) throw new ArgumentException("Skill file must contain UTF-8 text.");
        return content;
    }
}
