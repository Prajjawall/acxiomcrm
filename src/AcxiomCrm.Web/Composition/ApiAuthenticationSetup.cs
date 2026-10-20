using System.Text;
using System.Text.Json;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AcxiomCrm.Web.Composition;

public static class ApiAuthenticationSetup
{
    public const string SmartScheme = "Smart";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddScoped<Filters.ApiCsrfFilter>();

        services.PostConfigure<JwtOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.SigningKey))
            {
                if (environment.IsProduction())
                {
                    throw new InvalidOperationException("Security:Jwt:SigningKey must be configured in Production.");
                }
                options.SigningKey = "AcxiomCrmDevDefaultSigningKeyForJwtTokens_32BytesMinimumLength!";
            }
        });

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = SmartScheme;
            options.DefaultChallengeScheme = SmartScheme;
        })
        .AddPolicyScheme(SmartScheme, "Bearer or Cookie", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
                return IdentityConstants.ApplicationScheme;
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var traceId = context.HttpContext.TraceIdentifier;
                    var problem = new
                    {
                        type = "https://acxiomcrm.local/problems/unauthorized",
                        title = "Unauthorized",
                        status = 401,
                        detail = context.ErrorDescription ?? "A valid Bearer token is required to access this resource.",
                        traceId
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var traceId = context.HttpContext.TraceIdentifier;
                    var problem = new
                    {
                        type = "https://acxiomcrm.local/problems/forbidden",
                        title = "Forbidden",
                        status = 403,
                        detail = "You do not have permission to access this resource.",
                        traceId
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                }
            };
        });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                var keyBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwtOptions.SigningKey)
                    ? "AcxiomCrmDevDefaultSigningKeyForJwtTokens_32BytesMinimumLength!"
                    : jwtOptions.SigningKey);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}
