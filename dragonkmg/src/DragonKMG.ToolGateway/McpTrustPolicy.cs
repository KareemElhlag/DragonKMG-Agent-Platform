namespace DragonKMG.ToolGateway;

public sealed record McpToolDescriptor(string ServerId, string ToolId, string InputSchemaJson, bool Trusted);

public sealed class McpTrustPolicy(IReadOnlySet<string> trustedServers)
{
    public McpToolDescriptor AuthorizeDescriptor(string serverId, string toolId, string inputSchemaJson, bool explicitlyGranted)
    {
        if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(toolId)) throw new ArgumentException("MCP server and tool IDs are required.");
        if (!trustedServers.Contains(serverId)) throw new UnauthorizedAccessException("MCP server is not trusted.");
        if (!explicitlyGranted) throw new UnauthorizedAccessException("MCP tool is not granted to this agent.");
        if (!inputSchemaJson.TrimStart().StartsWith('{')) throw new InvalidOperationException("MCP input schema must be a JSON object.");
        return new McpToolDescriptor(serverId, toolId, inputSchemaJson, true);
    }
}
