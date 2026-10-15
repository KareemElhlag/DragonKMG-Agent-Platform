using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DragonKMG.Application;

namespace DragonKMG.ModelGateway;

public sealed class OpenAiCompatibleGateway(HttpClient client) : IModelGateway
{
    public async Task<ModelResponse> CompleteAsync(ModelRequest request)
    {
        var endpoint = new Uri(request.Endpoint, "v1/chat/completions");
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(new { model = request.ModelId, messages = new[] { new { role = "system", content = request.Instructions }, new { role = "user", content = request.Input } }, stream = false }) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        using var response = await client.SendAsync(message, request.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(request.CancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: request.CancellationToken);
        var text = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Provider returned no textual response.");
        var usage = json.RootElement.TryGetProperty("usage", out var usageElement) ? usageElement : default;
        return new ModelResponse(text, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var input) ? input.GetInt32() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var output) ? output.GetInt32() : null);
    }
}
