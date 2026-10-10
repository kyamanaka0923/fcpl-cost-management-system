using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CostManagement.WebApi;

/// <summary>
/// API authentication settings (section "Auth").
/// When <see cref="Authority"/> is set, every /api endpoint requires a valid JWT access token
/// issued by that authority (Amazon Cognito user pool in the AWS deployment).
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>
    /// Token issuer URL, e.g. https://cognito-idp.{region}.amazonaws.com/{userPoolId}.
    /// Empty = authentication disabled (allowed only in the Development environment).
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Optional app client ID. When set, only access tokens issued to this client
    /// (Cognito "client_id" claim) are accepted.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>Whether authentication is configured.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(Authority);
}

/// <summary>Registration of JWT bearer authentication for the API.</summary>
public static class AuthSetup
{
    /// <summary>
    /// Registers JWT bearer authentication when "Auth:Authority" is configured.
    /// Outside the Development environment a missing configuration is a startup error,
    /// so that the API is never deployed unauthenticated by mistake.
    /// </summary>
    public static AuthOptions AddCostManagementAuth(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? new AuthOptions();

        if (!options.Enabled)
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "認証設定(Auth:Authority)がありません。Development 以外の環境では認証の設定が必須です。");
            return options;
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                // Keep the original claim names (sub, username, client_id, token_use).
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Authority!.TrimEnd('/'),
                    // Cognito access tokens carry no "aud"; the client is checked via "client_id" below.
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "username",
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal!;
                        // Reject ID tokens: only access tokens may call the API.
                        if (principal.FindFirst("token_use")?.Value != "access")
                            context.Fail("access token is required");
                        else if (!string.IsNullOrWhiteSpace(options.ClientId)
                                 && principal.FindFirst("client_id")?.Value != options.ClientId)
                            context.Fail("token was issued to another client");
                        return Task.CompletedTask;
                    },
                };
            });
        builder.Services.AddAuthorization();
        return options;
    }
}
