using DragonKMG.Domain;

namespace DragonKMG.Application;

public static class ConnectionPolicy
{
    public static void Validate(ProviderConnection connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Name);
        if (!connection.Endpoint.IsAbsoluteUri || (connection.Endpoint.Scheme != Uri.UriSchemeHttps && !IsLoopbackHttp(connection.Endpoint)))
            throw new ArgumentException("Connections must use HTTPS, except an explicit loopback HTTP endpoint.", nameof(connection));
        if (!connection.SecretReference.StartsWith("env:", StringComparison.OrdinalIgnoreCase) || connection.SecretReference.Length <= 4)
            throw new ArgumentException("Secret references must use a non-empty env:NAME reference.", nameof(connection));
        if (connection.TrustZone == TrustZone.Cloud && !connection.CloudEgressAllowed && connection.Endpoint.IsLoopback)
            throw new ArgumentException("A loopback endpoint cannot be classified as cloud.", nameof(connection));
    }

    private static bool IsLoopbackHttp(Uri endpoint) => endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback;
}
