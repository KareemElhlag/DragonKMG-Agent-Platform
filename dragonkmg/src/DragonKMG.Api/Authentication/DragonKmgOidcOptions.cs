namespace DragonKMG.Api.Authentication;

/// <summary>
/// OIDC settings for validating API access tokens issued by the configured identity provider.
/// Secrets and signing keys are obtained from the provider's HTTPS discovery metadata.
/// </summary>
public sealed class DragonKmgOidcOptions
{
    public const string SectionName = "Authentication:Oidc";
    public const string SchemeName = "DragonKMG.OIDC";

    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? RequiredScope { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 60;
}
