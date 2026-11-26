using System.Security.Claims;
using System.Text.Encodings.Web;
using FactoryPulse.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactoryPulse.IntegrationTests;

internal static class TestSession
{
    internal static void AddMonitoringTestSession(this IServiceCollection services, string role = MonitoringAccess.Operator)
    {
        const string scheme = "MonitoringTestSession";
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = scheme;
            options.DefaultChallengeScheme = scheme;
            options.DefaultForbidScheme = scheme;
        }).AddScheme<AuthenticationSchemeOptions, Handler>(scheme, _ => { });
        services.AddSingleton(new TestRole(role));
    }

    internal static async Task<string> GetRequestTokenAsync(HttpClient client, CancellationToken token)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/auth/csrf", token);
        response.EnsureSuccessStatusCode();
        string cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);
    }

    internal static async Task<HttpResponseMessage> PostProtectedAsync(
        this HttpClient client, string route, CancellationToken token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = content };
        request.Headers.Add("X-XSRF-TOKEN", await GetRequestTokenAsync(client, token));
        return await client.SendAsync(request, token);
    }

    private sealed record TestRole(string Role);

    private sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TestRole role)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "monitoring-test"), new Claim(ClaimTypes.Role, role.Role)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
