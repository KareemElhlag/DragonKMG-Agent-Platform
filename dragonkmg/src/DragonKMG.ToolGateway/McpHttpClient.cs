using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DragonKMG.ToolGateway;

/// <summary>A server endpoint that an operator explicitly trusts in local configuration.</summary>
public sealed record McpHttpServer(string Id, Uri Endpoint, bool Enabled = true);

/// <summary>An explicit grant from an agent identity to one tool on one trusted MCP server.</summary>
public sealed record McpToolGrant(string AgentId, string ServerId, string ToolName);

/// <summary>
/// Immutable, static trust boundary for the bounded HTTP MCP client. Do not build this registry
/// from model output, project files, or remote discovery.
/// </summary>
public sealed class McpStaticRegistry
{
    private readonly IReadOnlyDictionary<string, McpHttpServer> _servers;
    private readonly HashSet<McpToolGrant> _grants;
    public int ServerCount => _servers.Count;

    public McpStaticRegistry(IEnumerable<McpHttpServer> servers, IEnumerable<McpToolGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(grants);

        var configured = servers.ToArray();
        if (configured.Any(server => string.IsNullOrWhiteSpace(server.Id) || server.Id.Length > 80))
            throw new ArgumentException("Every MCP server needs a valid identifier.", nameof(servers));
        if (configured.Select(server => server.Id).Distinct(StringComparer.Ordinal).Count() != configured.Length)
            throw new ArgumentException("MCP server identifiers must be unique.", nameof(servers));
        foreach (var server in configured)
            McpEndpointPolicy.Validate(server.Endpoint);

        _servers = configured.ToDictionary(server => server.Id, StringComparer.Ordinal);
        _grants = new HashSet<McpToolGrant>(grants);
    }

    internal McpHttpServer GetTrustedServer(string serverId)
    {
        if (!_servers.TryGetValue(serverId, out var server) || !server.Enabled)
            throw new UnauthorizedAccessException("MCP server is not explicitly trusted or enabled.");
        return server;
    }

    internal void DemandGrant(string agentId, string serverId, string toolName)
    {
        if (string.IsNullOrWhiteSpace(agentId) || !_grants.Contains(new McpToolGrant(agentId, serverId, toolName)))
            throw new UnauthorizedAccessException("MCP tool is not explicitly granted to this agent.");
    }
}

public sealed record McpRemoteTool(string Name, string? Description, string InputSchemaJson);

/// <summary>Safe, bounded failure surface; provider response bodies and endpoint details are never included.</summary>
public sealed class McpHttpException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Minimal Streamable HTTP MCP client. Supports initialize, tools/list (including cursors), and
/// tools/call. HTTP JSON and SSE responses are supported; all output bodies are bounded.
/// </summary>
public sealed class McpStreamableHttpClient : IDisposable
{
    private const string ProtocolVersion = "2025-03-26";
    private const int MaximumResponseBytes = 512 * 1024;
    private const int MaximumToolCount = 500;
    private const int MaximumPages = 20;
    private readonly HttpClient _http;
    private readonly McpStaticRegistry _registry;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<string, ServerSession> _sessions = new(StringComparer.Ordinal);
    private long _requestId;
    private bool _ownsClient;

    public McpStreamableHttpClient(McpStaticRegistry registry, TimeSpan? timeout = null)
        : this(CreateSafeClient(), registry, timeout, ownsClient: true)
    {
    }

    /// <summary>Injection overload intended for hosts and tests; redirects are still rejected.</summary>
    public McpStreamableHttpClient(HttpClient httpClient, McpStaticRegistry registry, TimeSpan? timeout = null)
        : this(httpClient, registry, timeout, ownsClient: false)
    {
    }

    private McpStreamableHttpClient(HttpClient httpClient, McpStaticRegistry registry, TimeSpan? timeout, bool ownsClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(timeout), "MCP timeout must be between zero and two minutes.");
        _ownsClient = ownsClient;
    }

    public async Task<IReadOnlyList<McpRemoteTool>> ListToolsAsync(string serverId, CancellationToken cancellationToken = default)
    {
        var server = _registry.GetTrustedServer(serverId);
        var session = _sessions.GetOrAdd(serverId, _ => new ServerSession());
        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(server, session, cancellationToken).ConfigureAwait(false);
            var tools = new List<McpRemoteTool>();
            string? cursor = null;
            for (var page = 0; page < MaximumPages; page++)
            {
                var parameters = cursor is null ? new JsonObject() : new JsonObject { ["cursor"] = cursor };
                using var result = await SendRequestAsync(server, session, "tools/list", parameters, cancellationToken).ConfigureAwait(false);
                var resultObject = result.RootElement;
                if (!resultObject.TryGetProperty("tools", out var toolArray) || toolArray.ValueKind != JsonValueKind.Array)
                    throw ProtocolError();

                foreach (var item in toolArray.EnumerateArray())
                {
                    if (tools.Count >= MaximumToolCount) throw new McpHttpException("too_many_tools", "MCP server returned too many tools.");
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                        throw ProtocolError();
                    var toolName = name.GetString();
                    if (!IsValidName(toolName)) throw ProtocolError();
                    string? description = item.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                        ? LimitText(desc.GetString(), 2048)
                        : null;
                    var schema = item.TryGetProperty("inputSchema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Object
                        ? schemaElement.GetRawText()
                        : "{\"type\":\"object\"}";
                    McpInputSchemaValidator.ValidateSchema(schema);
                    tools.Add(new McpRemoteTool(toolName!, description, schema));
                }

                cursor = resultObject.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String
                    ? next.GetString()
                    : null;
                if (string.IsNullOrEmpty(cursor)) return tools;
                if (cursor.Length > 2048) throw ProtocolError();
            }
            throw new McpHttpException("pagination_limit", "MCP tool discovery exceeded its page limit.");
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<JsonElement> CallToolAsync(
        string agentId,
        string serverId,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var server = _registry.GetTrustedServer(serverId);
        _registry.DemandGrant(agentId, serverId, toolName);
        if (!IsValidName(toolName)) throw new ArgumentException("MCP tool name is invalid.", nameof(toolName));
        if (arguments.ValueKind != JsonValueKind.Object) throw new McpHttpException("invalid_arguments", "MCP tool arguments must be a JSON object.");
        var argumentJson = arguments.GetRawText();
        if (Encoding.UTF8.GetByteCount(argumentJson) > 256 * 1024)
            throw new McpHttpException("invalid_arguments", "MCP tool arguments exceed the configured size limit.");

        var session = _sessions.GetOrAdd(serverId, _ => new ServerSession());
        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(server, session, cancellationToken).ConfigureAwait(false);
            using var listing = await SendRequestAsync(server, session, "tools/list", new JsonObject(), cancellationToken).ConfigureAwait(false);
            var tool = FindTool(listing.RootElement, toolName);
            if (tool is null)
            {
                // Cursor pages are uncommon for a direct call. Reuse the complete bounded discovery path.
                var discovered = await ListToolsUnderGateAsync(server, session, cancellationToken).ConfigureAwait(false);
                tool = discovered.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Name, toolName));
            }
            if (tool is null) throw new McpHttpException("tool_not_found", "The trusted MCP server does not expose this tool.");

            McpInputSchemaValidator.ValidateArguments(tool.InputSchemaJson, arguments);
            var parameters = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = JsonNode.Parse(argumentJson)
            };
            using var result = await SendRequestAsync(server, session, "tools/call", parameters, cancellationToken).ConfigureAwait(false);
            if (result.RootElement.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
                throw new McpHttpException("tool_failed", "The MCP tool reported that it could not complete the request.");
            return result.RootElement.Clone();
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task<IReadOnlyList<McpRemoteTool>> ListToolsUnderGateAsync(McpHttpServer server, ServerSession session, CancellationToken ct)
    {
        var tools = new List<McpRemoteTool>();
        string? cursor = null;
        for (var page = 0; page < MaximumPages; page++)
        {
            var parameters = cursor is null ? new JsonObject() : new JsonObject { ["cursor"] = cursor };
            using var result = await SendRequestAsync(server, session, "tools/list", parameters, ct).ConfigureAwait(false);
            var root = result.RootElement;
            if (!root.TryGetProperty("tools", out var array) || array.ValueKind != JsonValueKind.Array) throw ProtocolError();
            foreach (var item in array.EnumerateArray())
            {
                if (tools.Count >= MaximumToolCount) throw new McpHttpException("too_many_tools", "MCP server returned too many tools.");
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || !IsValidName(name.GetString())) throw ProtocolError();
                var schema = item.TryGetProperty("inputSchema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Object ? schemaElement.GetRawText() : "{\"type\":\"object\"}";
                McpInputSchemaValidator.ValidateSchema(schema);
                var description = item.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String ? LimitText(desc.GetString(), 2048) : null;
                tools.Add(new McpRemoteTool(name.GetString()!, description, schema));
            }
            cursor = root.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
            if (string.IsNullOrEmpty(cursor)) return tools;
            if (cursor.Length > 2048) throw ProtocolError();
        }
        throw new McpHttpException("pagination_limit", "MCP tool discovery exceeded its page limit.");
    }

    private async Task EnsureInitializedAsync(McpHttpServer server, ServerSession session, CancellationToken ct)
    {
        if (session.Initialized) return;
        using var result = await SendRequestAsync(server, session, "initialize", new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "DragonKMG", ["version"] = "1.0" }
        }, ct, initializing: true).ConfigureAwait(false);
        if (!result.RootElement.TryGetProperty("protocolVersion", out var version) || version.ValueKind != JsonValueKind.String)
            throw ProtocolError();
        session.ProtocolVersion = version.GetString();

        using var initialized = new HttpRequestMessage(HttpMethod.Post, server.Endpoint);
        AddMcpHeaders(initialized, session, "application/json, text/event-stream");
        initialized.Content = new StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await _http.SendAsync(initialized, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode)) throw new McpHttpException("redirect_blocked", "MCP server redirects are blocked.");
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Accepted or HttpStatusCode.NoContent))
                throw SafeStatusError(response.StatusCode);
            session.Initialized = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpHttpException("timeout", "MCP server did not respond before the timeout.");
        }
        catch (HttpRequestException)
        {
            throw new McpHttpException("connection_failed", "Could not connect to the trusted MCP server.");
        }
    }

    private async Task<JsonDocument> SendRequestAsync(McpHttpServer server, ServerSession session, string method, JsonObject parameters, CancellationToken ct, bool initializing = false)
    {
        var id = Interlocked.Increment(ref _requestId);
        var rpc = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters };
        using var request = new HttpRequestMessage(HttpMethod.Post, server.Endpoint);
        AddMcpHeaders(request, session, "application/json, text/event-stream");
        request.Content = new StringContent(rpc.ToJsonString(), Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode)) throw new McpHttpException("redirect_blocked", "MCP server redirects are blocked.");
            if (response.StatusCode == HttpStatusCode.NotFound && !initializing)
            {
                session.Initialized = false;
                session.SessionId = null;
                throw new McpHttpException("session_expired", "MCP server session expired. Reconnect and retry the operation.");
            }
            if (!response.IsSuccessStatusCode) throw SafeStatusError(response.StatusCode);
            if (response.Headers.TryGetValues("MCP-Session-Id", out var sessionIds))
            {
                var values = sessionIds.Take(2).ToArray();
                var value = values.Length == 1 ? values[0] : null;
                if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl)) throw ProtocolError();
                session.SessionId = value;
            }
            if (response.Headers.TryGetValues("MCP-Protocol-Version", out var versions))
            {
                var values = versions.Take(2).ToArray();
                var version = values.Length == 1 ? values[0] : null;
                if (string.IsNullOrWhiteSpace(version) || version.Length > 40 || version.Any(char.IsControl)) throw ProtocolError();
                session.ProtocolVersion = version;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                return await ReadSseResponseAsync(response, id, timeout.Token).ConfigureAwait(false);
            if (mediaType is not null && !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                throw new McpHttpException("invalid_content_type", "MCP server returned an unsupported response type.");
            var bytes = await ReadBoundedAsync(response.Content, MaximumResponseBytes, timeout.Token).ConfigureAwait(false);
            return ParseRpcResponse(bytes, id);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpHttpException("timeout", "MCP server did not respond before the timeout.");
        }
        catch (HttpRequestException)
        {
            throw new McpHttpException("connection_failed", "Could not connect to the trusted MCP server.");
        }
    }

    private async Task<JsonDocument> ReadSseResponseAsync(HttpResponseMessage response, long expectedId, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var readBuffer = new byte[8192];
        using var lineBuffer = new MemoryStream();
        var eventData = new StringBuilder();
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(readBuffer.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > MaximumResponseBytes) throw new McpHttpException("response_too_large", "MCP server response exceeded the configured size limit.");
            for (var index = 0; index < read; index++)
            {
                var current = readBuffer[index];
                if (current != (byte)'\n')
                {
                    if (lineBuffer.Length >= MaximumResponseBytes)
                        throw new McpHttpException("response_too_large", "MCP server response exceeded the configured size limit.");
                    lineBuffer.WriteByte(current);
                    continue;
                }

                var line = Encoding.UTF8.GetString(lineBuffer.GetBuffer(), 0, checked((int)lineBuffer.Length)).TrimEnd('\r');
                lineBuffer.SetLength(0);
                if (line.Length == 0)
                {
                    if (eventData.Length == 0) continue;
                    var matching = ParseSseEvent(eventData.ToString(), expectedId);
                    eventData.Clear();
                    if (matching is not null) return matching;
                    continue;
                }
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (eventData.Length > 0) eventData.Append('\n');
                    eventData.Append(line.AsSpan(5).TrimStart());
                    if (eventData.Length > MaximumResponseBytes)
                        throw new McpHttpException("response_too_large", "MCP server response exceeded the configured size limit.");
                }
            }
        }

        if (lineBuffer.Length > 0)
        {
            var finalLine = Encoding.UTF8.GetString(lineBuffer.GetBuffer(), 0, checked((int)lineBuffer.Length)).TrimEnd('\r');
            if (finalLine.StartsWith("data:", StringComparison.Ordinal))
            {
                if (eventData.Length > 0) eventData.Append('\n');
                eventData.Append(finalLine.AsSpan(5).TrimStart());
            }
        }
        if (eventData.Length > 0)
        {
            var matching = ParseSseEvent(eventData.ToString(), expectedId);
            if (matching is not null) return matching;
        }
        throw ProtocolError();
    }

    private static JsonDocument? ParseSseEvent(string payload, long expectedId)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
            return MatchesResponseId(document.RootElement, expectedId)
                ? ParseRpcResponse(Encoding.UTF8.GetBytes(payload), expectedId)
                : null;
        }
        catch (JsonException)
        {
            throw ProtocolError();
        }
    }

    private static JsonDocument ParseRpcResponse(byte[] bytes, long expectedId)
    {
        try
        {
            var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            if (!MatchesResponseId(document.RootElement, expectedId))
            {
                document.Dispose();
                throw ProtocolError();
            }
            if (document.RootElement.TryGetProperty("error", out _))
            {
                document.Dispose();
                throw new McpHttpException("remote_error", "MCP server returned an error for the requested operation.");
            }
            if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw ProtocolError();
            }
            var resultJson = result.GetRawText();
            document.Dispose();
            return JsonDocument.Parse(resultJson, new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException)
        {
            throw ProtocolError();
        }
    }

    private static bool MatchesResponseId(JsonElement root, long expectedId) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("jsonrpc", out var version) && version.ValueKind == JsonValueKind.String && version.GetString() == "2.0" &&
        root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number) && number == expectedId;

    private static McpRemoteTool? FindTool(JsonElement listResult, string name)
    {
        if (!listResult.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array) throw ProtocolError();
        foreach (var item in tools.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var remoteName) || remoteName.ValueKind != JsonValueKind.String) throw ProtocolError();
            if (!StringComparer.Ordinal.Equals(remoteName.GetString(), name)) continue;
            var schema = item.TryGetProperty("inputSchema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Object ? schemaElement.GetRawText() : "{\"type\":\"object\"}";
            McpInputSchemaValidator.ValidateSchema(schema);
            var description = item.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String ? LimitText(desc.GetString(), 2048) : null;
            return new McpRemoteTool(name, description, schema);
        }
        return null;
    }

    private static void AddMcpHeaders(HttpRequestMessage request, ServerSession session, string accept)
    {
        request.Headers.Accept.ParseAdd(accept);
        if (!string.IsNullOrEmpty(session.SessionId)) request.Headers.TryAddWithoutValidation("MCP-Session-Id", session.SessionId);
        if (!string.IsNullOrEmpty(session.ProtocolVersion)) request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", session.ProtocolVersion);
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumBytes, CancellationToken ct)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maximumBytes)
            throw new McpHttpException("response_too_large", "MCP server response exceeded the configured size limit.");
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(maximumBytes, 16 * 1024));
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumBytes) throw new McpHttpException("response_too_large", "MCP server response exceeded the configured size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static HttpClient CreateSafeClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    }) { Timeout = Timeout.InfiniteTimeSpan };

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;
    private static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 128 && !name.Any(char.IsControl);
    private static string? LimitText(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
    private static McpHttpException ProtocolError() => new("invalid_protocol_response", "MCP server returned an invalid protocol response.");
    private static McpHttpException SafeStatusError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new("server_rejected", "MCP server rejected the request."),
        (HttpStatusCode)429 => new("rate_limited", "MCP server is rate limiting requests."),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => new("timeout", "MCP server did not respond before the timeout."),
        _ => new("server_unavailable", "MCP server could not complete the request.")
    };

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
        foreach (var session in _sessions.Values) session.Gate.Dispose();
        _sessions.Clear();
    }

    private sealed class ServerSession
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string? SessionId { get; set; }
        public string? ProtocolVersion { get; set; }
        public bool Initialized { get; set; }
    }
}

internal static class McpEndpointPolicy
{
    public static void Validate(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("MCP endpoint must be an absolute URL without credentials or fragments.", nameof(endpoint));
        var loopback = endpoint.IsLoopback || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && loopback))
            throw new ArgumentException("MCP endpoints must use HTTPS, or HTTP on loopback only.", nameof(endpoint));
        if (endpoint.IsDefaultPort is false && (endpoint.Port < 1 || endpoint.Port > 65535))
            throw new ArgumentException("MCP endpoint port is invalid.", nameof(endpoint));
    }
}
