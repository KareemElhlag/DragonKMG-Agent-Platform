using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.ModelGateway;

public sealed class OpenAiCompatibleGateway(HttpClient client) : IModelGateway, IProviderCatalog
{
    public async Task<ModelResponse> CompleteAsync(ModelRequest request)
    {
        var kind = request.Kind;
        var operation = kind switch
        {
            ConnectionKind.Ollama => "chat",
            ConnectionKind.Anthropic => "messages",
            ConnectionKind.Gemini => "models/" + Uri.EscapeDataString(ModelIdentifier.Canonical(kind, request.ModelId)) + ":generateContent",
            _ => "chat/completions"
        };
        var messages = new[] { new { role = "system", content = request.Instructions }, new { role = "user", content = request.Input } };
        object payload;
        if (kind == ConnectionKind.Ollama)
            payload = new { model = request.ModelId, messages, stream = false, options = new { num_predict = request.MaxOutputTokens ?? 2048 } };
        else if (kind == ConnectionKind.Anthropic)
            payload = new { model = request.ModelId, system = request.Instructions, messages = new[] { new { role = "user", content = request.Input } }, max_tokens = request.MaxOutputTokens ?? 2048 };
        else if (kind == ConnectionKind.Gemini)
            payload = new { systemInstruction = new { parts = new[] { new { text = request.Instructions } } }, contents = new[] { new { role = "user", parts = new[] { new { text = request.Input } } } }, generationConfig = new { maxOutputTokens = request.MaxOutputTokens ?? 2048 } };
        else
        {
            var body = new Dictionary<string, object> { ["model"] = request.ModelId, ["messages"] = messages, ["stream"] = false };
            if (request.MaxOutputTokens is int max) body["max_tokens"] = max;
            payload = body;
        }
        using var message = CreateRequest(HttpMethod.Post, ProviderEndpoint.Build(request.Endpoint, kind, operation), kind, request.ApiKey);
        message.Content = JsonContent.Create(payload);
        using var json = await SendJsonAsync(message, request.CancellationToken);
        var root = json.RootElement;
        string? text;
        JsonElement usage = default;
        string inputKey = "prompt_tokens", outputKey = "completion_tokens";
        if (kind == ConnectionKind.Ollama)
        {
            text = root.GetProperty("message").GetProperty("content").GetString();
            usage = root; inputKey = "prompt_eval_count"; outputKey = "eval_count";
        }
        else if (kind == ConnectionKind.Anthropic)
        {
            text = string.Join("\n", root.GetProperty("content").EnumerateArray().Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text").Select(block => block.GetProperty("text").GetString()));
            if (root.TryGetProperty("usage", out var value)) usage = value;
            inputKey = "input_tokens"; outputKey = "output_tokens";
        }
        else if (kind == ConnectionKind.Gemini)
        {
            text = string.Join("\n", root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts").EnumerateArray().Where(part => part.TryGetProperty("text", out _)).Select(part => part.GetProperty("text").GetString()));
            if (root.TryGetProperty("usageMetadata", out var value)) usage = value;
            inputKey = "promptTokenCount"; outputKey = "candidatesTokenCount";
        }
        else
        {
            text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (root.TryGetProperty("usage", out var value)) usage = value;
        }
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Provider returned no textual response.");
        return new ModelResponse(text, Count(usage, inputKey), Count(usage, outputKey));
    }

    public async Task<IReadOnlyList<string>> DiscoverAsync(ProviderConnection connection, string secret, CancellationToken ct)
    {
        ConnectionPolicy.Validate(connection);
        var endpoint = ProviderEndpoint.Build(connection.Endpoint, connection.Kind, connection.Kind == ConnectionKind.Ollama ? "tags" : "models");
        var models = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < 10; page++)
        {
            using var message = CreateRequest(HttpMethod.Get, endpoint, connection.Kind, secret);
            using var json = await SendJsonAsync(message, ct);
            var root = json.RootElement;
            var array = root.GetProperty(connection.Kind is ConnectionKind.Ollama or ConnectionKind.Gemini ? "models" : "data");
            foreach (var model in array.EnumerateArray())
            {
                if (connection.Kind == ConnectionKind.Gemini && (!model.TryGetProperty("supportedGenerationMethods", out var methods) || !methods.EnumerateArray().Any(method => method.GetString() == "generateContent"))) continue;
                var id = model.GetProperty(connection.Kind is ConnectionKind.Ollama or ConnectionKind.Gemini ? "name" : "id").GetString();
                if (connection.Kind == ConnectionKind.Gemini && id?.StartsWith("models/", StringComparison.Ordinal) == true) id = id[7..];
                if (!string.IsNullOrWhiteSpace(id) && id.Length <= 256 && !id.Any(char.IsControl)) models.Add(id);
                if (models.Count >= 2000) return models.Order().ToArray();
            }
            var next = root.TryGetProperty("nextPageToken", out var token) ? token.GetString() : null;
            if (!string.IsNullOrEmpty(next)) endpoint = new UriBuilder(endpoint) { Query = "pageToken=" + Uri.EscapeDataString(next) }.Uri;
            else if (connection.Kind == ConnectionKind.Anthropic && root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True && root.TryGetProperty("last_id", out var last) && !string.IsNullOrWhiteSpace(last.GetString()))
                endpoint = new UriBuilder(endpoint) { Query = "after_id=" + Uri.EscapeDataString(last.GetString()!) }.Uri;
            else break;
        }
        return models.Order().ToArray();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, ConnectionKind kind, string secret)
    {
        var message = new HttpRequestMessage(method, uri);
        if (kind == ConnectionKind.Anthropic)
        {
            message.Headers.Add("anthropic-version", "2023-06-01");
            if (!string.IsNullOrEmpty(secret)) message.Headers.Add("x-api-key", secret);
        }
        else if (kind == ConnectionKind.Gemini)
        {
            if (!string.IsNullOrEmpty(secret)) message.Headers.Add("x-goog-api-key", secret);
        }
        else if (!string.IsNullOrEmpty(secret)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return message;
    }

    private async Task<JsonDocument> SendJsonAsync(HttpRequestMessage message, CancellationToken ct)
    {
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > 4 * 1024 * 1024) throw new InvalidOperationException("Provider response exceeds 4 MiB.");
            buffer.Write(chunk, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
    private static int? Count(JsonElement usage, string key) => usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(key, out var value) && value.TryGetInt32(out var count) ? count : null;
}
