using DragonKMG.Application;
namespace DragonKMG.Cli;

internal static class TerminalGitHubCommands
{
    public static async Task<string> ExecuteAsync(IGitHubIntegration service, Guid? projectId, string argument, CancellationToken ct)
    {
        var parts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        switch (parts.FirstOrDefault()?.ToLowerInvariant() ?? "status")
        {
            case "status": var status = await service.StatusAsync(ct); return status.Connected ? "GitHub: " + status.Login + " · read-only" : "GitHub disconnected. /github connect (fine-grained PAT, Windows DPAPI).";
            case "connect":
                if (parts.Length != 1) return "Do not put tokens in commands. Use /github connect and the masked prompt.";
                if (Console.IsInputRedirected) return "GitHub connect requires an interactive terminal.";
                Console.Write("GitHub fine-grained PAT (hidden): "); var token = ReadSecret();
                var connected = await service.ConnectAsync(token, ct); return "GitHub connected: " + connected.Login + " · read-only, no tool grants.";
            case "disconnect": await service.DisconnectAsync(ct); return "GitHub credentials and project bindings removed locally. Revoke the PAT in GitHub settings if no longer needed.";
            case "repos":
                if (parts.Length > 1 && !int.TryParse(parts[1], out _)) return "Usage: /github repos [page]";
                var repos = await service.RepositoriesAsync(parts.Length > 1 ? int.Parse(parts[1]) : 1, ct);
                return repos.Count == 0 ? "No accessible repositories on this page." : string.Join('\n', repos.Select(repo => repo.FullName + (repo.Private ? " · private" : " · public")));
            case "use":
                if (projectId is null) return "Select a project directory first: /project cwd or /project use <name>.";
                if (parts.Length < 2) return "Usage: /github use owner/repository";
                return "Project repository: " + (await service.BindAsync(projectId.Value, parts[1], ct)).FullName + " · metadata only; no clone or file reads.";
            default: return "/github status | connect | repos [page] | use owner/repository | disconnect";
        }
    }
    private static string ReadSecret()
    {
        var value = new System.Text.StringBuilder();
        while (true) { var key = Console.ReadKey(true); if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); } if (key.Key == ConsoleKey.Escape) { Console.WriteLine(); return ""; } if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; } else if (!char.IsControl(key.KeyChar) && value.Length < 512) value.Append(key.KeyChar); }
    }
}
