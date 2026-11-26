using System.Threading.RateLimiting;
using FactoryPulse.Infrastructure.Identity;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Security;

internal static class IdentityRegistration
{
    internal const string LoginLimit = "monitoring-login";
    internal const string RequestTokenCookie = "XSRF-TOKEN";

    internal static void AddMonitoringIdentity(this IServiceCollection services)
    {
        services.AddOptions<AccessOptions>().BindConfiguration("Access").ValidateDataAnnotations().ValidateOnStart();
        services.AddIdentity<MonitoringUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<FactoryPulseDbContext>();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AccessOptions>>((cookie, access) =>
            {
                ConfigureCookie(cookie.Cookie, "__Host-FactoryPulse");
                cookie.ExpireTimeSpan = TimeSpan.FromMinutes(access.Value.SessionMinutes);
                cookie.SlidingExpiration = false;
                cookie.Events.OnRedirectToLogin = context => SetStatus(context, StatusCodes.Status401Unauthorized);
                cookie.Events.OnRedirectToAccessDenied = context => SetStatus(context, StatusCodes.Status403Forbidden);
                cookie.Events.OnValidatePrincipal = ValidateSessionAsync;
            });

        services.AddAntiforgery(options =>
        {
            ConfigureCookie(options.Cookie, "__Host-FactoryPulse-Csrf");
            options.HeaderName = "X-XSRF-TOKEN";
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
        services.AddMonitoringPolicies();
    }

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInManager<MonitoringUser>>();
        if (await signIn.ValidateSecurityStampAsync(context.Principal) is not { IsActive: true })
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    private static Task SetStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static void ConfigureCookie(CookieBuilder cookie, string name)
    {
        cookie.Name = name;
        cookie.Path = "/";
        cookie.HttpOnly = true;
        cookie.SameSite = SameSiteMode.Strict;
        cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.IsEssential = true;
    }
}
