using DragonKMG.Domain;

namespace DragonKMG.Application;

public sealed class TeamConflictException : Exception;
public sealed class TeamRoutingException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class TeamPolicy
{
    public static void Validate(TeamDefinition team)
    {
        if (team.Id == Guid.Empty || string.IsNullOrWhiteSpace(team.Name) || team.Name != team.Name.Trim() || team.Name.Length > 100 || team.Name.Any(char.IsControl) || team.Description is null || team.Description.Length > 4000 || team.Version < 1 || team.AgentIds is null || team.AgentIds.Count is < 1 or > 16 || team.AgentIds.Any(id => id == Guid.Empty) || team.AgentIds.Distinct().Count() != team.AgentIds.Count)
            throw new ArgumentException("Invalid team configuration.");
    }

    public static async Task<TeamDefinition> FindAvailableAsync(IPlatformStore store, Guid teamId, CancellationToken ct)
    {
        var team = (await store.ListTeamsAsync(ct)).FirstOrDefault(value => value.Id == teamId);
        if (team is null || !team.Enabled) throw new TeamRoutingException("TeamUnavailable");
        Validate(team);
        foreach (var agentId in team.AgentIds)
            if (await store.FindAgentAsync(agentId, ct) is not { Enabled: true }) throw new TeamRoutingException("TeamUnavailable");
        return team;
    }
}
