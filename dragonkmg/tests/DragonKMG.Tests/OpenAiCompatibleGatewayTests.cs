using System.Net;
using System.Text;
using DragonKMG.Application;
using DragonKMG.ModelGateway;

namespace DragonKMG.Tests;

public sealed class OpenAiCompatibleGatewayTests
{
    [Fact]
    public async Task ParsesAProviderResponseWithoutLeakingTheApiKeyAsync()
    {
        // Arrange
        using var client = new HttpClient(new StaticResponseHandler("""{"choices":[{"message":{"content":"verified response"}}],"usage":{"prompt_tokens":5,"completion_tokens":3}}"""));
        var gateway = new OpenAiCompatibleGateway(client);
        var request = new ModelRequest(new Uri("https://provider.example/"), "sensitive-value", "model-a", "system", "hello", CancellationToken.None);

        // Act
        var response = await gateway.CompleteAsync(request);

        // Assert
        Assert.Equal("verified response", response.Text);
        Assert.Equal(5, response.InputTokens);
        Assert.Equal(3, response.OutputTokens);
    }

    private sealed class StaticResponseHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
    }
}
