using DragonKMG.Domain;

namespace DragonKMG.ModelGateway;

public static class ProviderEndpoint
{
    public static Uri Build(Uri endpoint, ConnectionKind kind, string operation)
    {
        var path = endpoint.AbsolutePath.TrimEnd('/');
        var version = kind == ConnectionKind.Gemini ? "v1beta" : kind == ConnectionKind.Ollama ? "api" : "v1";
        foreach (var suffix in new[] { "/chat/completions", "/messages", "/models", "/chat", "/tags" })
            if (path.EndsWith(suffix, StringComparison.Ordinal)) { path = path[..^suffix.Length]; break; }
        if (!path.EndsWith("/" + version, StringComparison.Ordinal) && !(kind == ConnectionKind.Gemini && path.EndsWith("/v1", StringComparison.Ordinal)))
            path += "/" + version;
        return new UriBuilder(endpoint) { Path = path + "/" + operation, Query = "", Fragment = "" }.Uri;
    }
}
