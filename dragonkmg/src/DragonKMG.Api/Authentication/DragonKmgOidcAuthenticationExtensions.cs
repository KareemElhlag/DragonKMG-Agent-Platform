using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DragonKMG.Api.Authentication;

/// <summary>
/// Registers strict JWT validation for an OIDC authority without replacing an application's
/// existing default authentication scheme. The local development-token middleware can therefore
/// remain a separate development-only path while the host opts API routes into this scheme.
/// </summary>
public static class DragonKmgOidcAuthenticationExtensions
{
    public static IServiceCollection AddDragonKmgOidcAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(DragonKmgOidcOptions.SectionName);
        var options = section.Get<DragonKmgOidcOptions>() ?? new DragonKmgOidcOptions();

        if (!options.Enabled)
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    $"{DragonKmgOidcOptions.SectionName}:Enabled must be true in Production. " +
                    "Refusing to start the API without configured OIDC authentication.");
            }

            // Local development keeps using the existing local-token middleware.
            return services;
        }

        Validate(options, environment);

        services.AddOptions<DragonKmgOidcOptions>()
            .Bind(section)
            .Validate(value => IsValid(value, environment), BuildValidationMessage())
            .ValidateOnStart();

        // Register the named scheme only. The host must explicitly select it for protected
        // endpoints/policies so this extension does not silently change local-token behavior.
        services.AddAuthentication()
            .AddJwtBearer(DragonKmgOidcOptions.SchemeName, bearer =>
            {
                bearer.Authority = options.Authority;
                bearer.RequireHttpsMetadata = options.RequireHttpsMetadata;
                bearer.MapInboundClaims = false;
                bearer.SaveToken = false;
                bearer.RefreshOnIssuerKeyNotFound = true;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        return services;
    }

    private static void Validate(DragonKmgOidcOptions options, IHostEnvironment environment)
    {
        if (!IsValid(options, environment))
        {
            throw new InvalidOperationException(BuildValidationMessage());
        }
    }

    private static bool IsValid(DragonKmgOidcOptions options, IHostEnvironment environment)
    {
        if (!options.Enabled ||
            !IsHttpsUri(options.Authority) ||
            !IsHttpsUri(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            options.Audience.Length > 512 ||
            options.RequiredScope is { Length: > 256 } ||
            (environment.IsProduction() && string.IsNullOrWhiteSpace(options.RequiredScope)) ||
            options.ClockSkewSeconds is < 0 or > 300)
        {
            return false;
        }

        return !environment.IsProduction() || options.RequireHttpsMetadata;
    }

    private static bool IsHttpsUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Fragment);

    private static string BuildValidationMessage() =>
        $"Invalid {DragonKmgOidcOptions.SectionName} configuration. When enabled, Authority and Issuer " +
        "must be absolute HTTPS URLs, Audience must be non-empty, ClockSkewSeconds must be between 0 and 300, " +
            "Production also requires a bounded RequiredScope, and RequireHttpsMetadata must remain enabled.";
}
