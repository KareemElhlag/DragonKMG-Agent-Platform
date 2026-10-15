namespace DragonKMG.Cli;

public static class TerminalInput
{
    public static (string Command, string Argument) Parse(string line)
    {
        line = line.Trim();
        if (!line.StartsWith('/')) return ("__prompt", line);
        var parts = line[1..].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts.FirstOrDefault()?.ToLowerInvariant() ?? "help", parts.ElementAtOrDefault(1) ?? "");
    }
}
