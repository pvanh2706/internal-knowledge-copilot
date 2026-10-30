using System.Text;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Authentication;

public static class AuthenticationSetup
{
    // TenantJwt là scheme mặc định cho mọi endpoint tenant hiện có. PlatformJwt chỉ được dùng qua policy platform;
    // không policy nào chấp nhận cả hai scheme.
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, JwtOptions tenant, PlatformJwtOptions platform)
    {
        services
            .AddAuthentication(AppAuthenticationSchemes.TenantJwt)
            .AddJwtBearer(AppAuthenticationSchemes.TenantJwt, options =>
            {
                options.TokenValidationParameters = CreateValidationParameters(tenant.Issuer, tenant.Audience, tenant.SigningKey);
                options.Events = new JwtBearerEvents { OnTokenValidated = TenantPrincipalValidator.ValidateAsync };
            })
            .AddJwtBearer(AppAuthenticationSchemes.PlatformJwt, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateValidationParameters(platform.Issuer, platform.Audience, platform.SigningKey);
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.RoleClaimType = "role";
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
                options.Events = new JwtBearerEvents { OnTokenValidated = PlatformPrincipalValidator.ValidateAsync };
            });

        services.AddAuthorization(options =>
        {
            var tenantAuthenticated = new AuthorizationPolicyBuilder(AppAuthenticationSchemes.TenantJwt)
                .RequireAuthenticatedUser()
                .RequireClaim(PrincipalClaimTypes.PrincipalType, PrincipalTypes.Tenant)
                .RequireClaim(TenantClaimTypes.TenantId)
                .RequireClaim(TenantClaimTypes.TenantCode)
                .Build();
            options.AddPolicy(AppAuthorizationPolicies.TenantAuthenticated, tenantAuthenticated);
            options.AddPolicy(AppAuthorizationPolicies.TenantAdminOnly, policy => policy
                .Combine(tenantAuthenticated)
                .RequireRole(nameof(UserRole.Admin)));

            // [Authorize] không tham số trên endpoint tenant chỉ chấp nhận principal tenant đã xác thực.
            options.DefaultPolicy = tenantAuthenticated;

            options.AddPolicy(AppAuthorizationPolicies.PlatformAdminOnly, policy => policy
                .AddAuthenticationSchemes(AppAuthenticationSchemes.PlatformJwt)
                .RequireAuthenticatedUser()
                .RequireClaim(PrincipalClaimTypes.PrincipalType, PrincipalTypes.Platform)
                .RequireClaim(PrincipalClaimTypes.PlatformSession, PlatformSessionTypes.Full));
            options.AddPolicy(AppAuthorizationPolicies.PlatformPasswordChange, policy => policy
                .AddAuthenticationSchemes(AppAuthenticationSchemes.PlatformJwt)
                .RequireAuthenticatedUser()
                .RequireClaim(PrincipalClaimTypes.PrincipalType, PrincipalTypes.Platform)
                .RequireClaim(PrincipalClaimTypes.PlatformSession, PlatformSessionTypes.Full, PlatformSessionTypes.PasswordChangeOnly));
        });
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, PlatformAuthorizationAuditHandler>();

        return services;
    }

    private static TokenValidationParameters CreateValidationParameters(string issuer, string audience, string signingKey)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        };
    }
}
