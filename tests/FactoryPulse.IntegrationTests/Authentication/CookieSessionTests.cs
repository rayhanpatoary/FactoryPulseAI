using System.Net;
using System.Net.Http.Json;
using FactoryPulse.Api.Controllers;
using FactoryPulse.Api.Security;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace FactoryPulse.IntegrationTests.Authentication;

public sealed class CookieSessionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoginRequiresCsrfAndIssuesAnExplicitSecureSession()
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token);
        await host.CreateUserAsync(MonitoringAccess.Viewer, Token);
        using HttpResponseMessage missingToken = await host.Client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("test-user", host.Password), Token);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        using HttpResponseMessage anonymous = await host.Client.GetAsync("/api/machines", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        using HttpResponseMessage login = await host.LoginAsync(Token);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        SetCookieHeaderValue cookie = Assert.Single(login.Headers.GetValues("Set-Cookie")
            .Select(value => SetCookieHeaderValue.Parse(value)), value => value.Name == "__Host-FactoryPulse");
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/", cookie.Path.Value);
        SessionUser? user = await host.Client.GetFromJsonAsync<SessionUser>("/api/auth/me", Token);
        Assert.NotNull(user);
        Assert.Equal(["Viewer"], user.Roles);
        Assert.False(user.CanAcknowledge);
        Assert.NotNull(user.ExpiresAtUtc);

        host.Clock.Advance(TimeSpan.FromMinutes(31));
        using HttpResponseMessage expired = await host.Client.GetAsync("/api/auth/me", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentialsLockTheAccountWithoutDisclosingItsExistence()
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token);
        await host.CreateUserAsync(MonitoringAccess.Viewer, Token);
        using HttpResponseMessage unknown = await host.LoginAsync(Token, userName: "unknown");
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        for (int count = 0; count < 5; count++)
        {
            using HttpResponseMessage incorrect = await host.LoginAsync(Token, host.Password + "x");
            Assert.Equal(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        }

        using HttpResponseMessage locked = await host.LoginAsync(Token);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.DoesNotContain("Set-Cookie", locked.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task DeactivationInvalidatesExistingCookiesAndPreventsLogin()
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token);
        await host.CreateUserAsync(MonitoringAccess.Viewer, Token);
        using HttpResponseMessage login = await host.LoginAsync(Token);
        login.EnsureSuccessStatusCode();
        await host.DisableUserAsync();
        using HttpResponseMessage disabled = await host.Client.GetAsync("/api/machines", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        using HttpResponseMessage retry = await host.LoginAsync(Token);
        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
    }
}
