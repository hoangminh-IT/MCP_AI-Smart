using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Authentication;

public static class DependencyInjectionAuthenticationExtensions
{
    public static IServiceCollection AddInventoryAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(options => options.HasValidSigningKey(),
                "Jwt:SigningKey must contain at least 32 UTF-8 bytes. Configure it using user secrets or environment variables.")
            .Validate(options => options.AccessTokenMinutes is >= 5 and <= 120,
                "Jwt:AccessTokenMinutes must be between 5 and 120.")
            .ValidateOnStart();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var settings = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(15),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateCurrentUserAsync,
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await WriteAuthProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            "Unauthorized", "A valid sign-in token is required.");
                    },
                    OnForbidden = context => WriteAuthProblemAsync(context.HttpContext,
                        StatusCodes.Status403Forbidden, "Forbidden", "Your account does not have permission for this action.")
                };
            });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }

    private static async Task ValidateCurrentUserAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null ||
            !int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0 ||
            !int.TryParse(principal.FindFirstValue("token_version"),
                NumberStyles.None, CultureInfo.InvariantCulture, out var tokenVersion))
        {
            context.Fail("The token identity is invalid.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Id == userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role) ||
            user.TokenVersion != tokenVersion ||
            user.Username != principal.FindFirstValue(ClaimTypes.Name) ||
            user.Role != principal.FindFirstValue(ClaimTypes.Role))
        {
            context.Fail("The account or token is no longer valid.");
        }
    }

    private static async Task WriteAuthProblemAsync(HttpContext context, int status,
        string title, string detail)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
