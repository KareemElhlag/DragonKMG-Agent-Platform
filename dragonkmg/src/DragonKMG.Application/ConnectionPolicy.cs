using DragonKMG.Domain;

namespace DragonKMG.Application;

public static class ConnectionPolicy
{
    public static void Validate(ProviderConnection connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Name);
        if (!Enum.IsDefined(connection.Kind) || !Enum.IsDefined(connection.TrustZone)) throw new ArgumentException("Unsupported protocol or trust zone.");
        if (!connection.Endpoint.IsAbsoluteUri || (connection.Endpoint.Scheme != Uri.UriSchemeHttps && !IsLoopbackHttp(connection.Endpoint)))
            throw new ArgumentException("Connections must use HTTPS, except an explicit loopback HTTP endpoint.", nameof(connection));
        if (!string.IsNullOrEmpty(connection.Endpoint.UserInfo) || !string.IsNullOrEmpty(connection.Endpoint.Query) || !string.IsNullOrEmpty(connection.Endpoint.Fragment))
            throw new ArgumentException("Base URLs must not contain credentials, query strings or fragments.");
        if (!string.IsNullOrEmpty(connection.SecretReference) && ((!connection.SecretReference.StartsWith("env:", StringComparison.OrdinalIgnoreCase) && !connection.SecretReference.StartsWith("vault:", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrWhiteSpace(connection.SecretReference[(connection.SecretReference.IndexOf(':') + 1)..])))
            throw new ArgumentException("Secret references must use a non-empty env:NAME or vault:NAME reference.", nameof(connection));
        if (connection.TrustZone == TrustZone.Cloud && !connection.CloudEgressAllowed && connection.Endpoint.IsLoopback)
            throw new ArgumentException("A loopback endpoint cannot be classified as cloud.", nameof(connection));
        if (connection.TrustZone == TrustZone.Local && !connection.Endpoint.IsLoopback)
            throw new ArgumentException("Local connections must point to a loopback address. Select Remote or Cloud for a remote URL.");
    }

    private static bool IsLoopbackHttp(Uri endpoint) => endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback;
}
