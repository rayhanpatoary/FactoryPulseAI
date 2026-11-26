using System.Net;
using System.Net.Http.Json;
using FactoryPulse.Api.Controllers;
using FactoryPulse.Api.Security;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace FactoryPulse.IntegrationTests.Authentication;

public sealed class AuthorizedMonitoringTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RepeatedLoginRequestsAreRateLimited()
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage login = await host.LoginAsync(Token, userName: "unregistered");
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        using HttpResponseMessage throttled = await host.LoginAsync(Token, userName: "unregistered");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Theory]
    [InlineData(MonitoringAccess.Viewer, HttpStatusCode.Forbidden, false)]
    [InlineData(MonitoringAccess.Operator, HttpStatusCode.NotFound, true)]
    [InlineData(MonitoringAccess.Administrator, HttpStatusCode.NotFound, true)]
    public async Task ProductionEnforcesMonitoringAndAcknowledgementRoles(string role, HttpStatusCode status, bool canAcknowledge)
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token);
        await host.CreateUserAsync(role, Token);
        using HttpResponseMessage denied = await host.Client.PostAsync("/api/alarms/1/acknowledge", null, Token);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using HttpResponseMessage login = await host.LoginAsync(Token);
        login.EnsureSuccessStatusCode();
        SessionUser? session = await login.Content.ReadFromJsonAsync<SessionUser>(Token);
        Assert.NotNull(session);
        Assert.Equal(canAcknowledge, session.CanAcknowledge);
        using HttpResponseMessage telemetry = await host.Client.GetAsync("/api/machines", Token);
        Assert.Equal(HttpStatusCode.OK, telemetry.StatusCode);
        if (canAcknowledge)
        {
            using HttpResponseMessage missingCsrf = await host.Client.PostAsync("/api/alarms/1/acknowledge", null, Token);
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        }

        using HttpResponseMessage acknowledgement = await host.Client.PostProtectedAsync("/api/alarms/1/acknowledge", Token);
        Assert.Equal(status, acknowledgement.StatusCode);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    public async Task ProductionNegotiationRequiresOptInAndAuthentication(bool enable, HttpStatusCode status)
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token, enable);
        using HttpResponseMessage negotiation = await host.Client.PostAsync("/hubs/monitoring/negotiate?negotiateVersion=1", null, Token);
        Assert.Equal(status, negotiation.StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesTheCookieAndDisconnectsAnAuthenticatedProductionHub()
    {
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(Token, allowProduction: true);
        await host.CreateUserAsync(MonitoringAccess.Viewer, Token);
        using HttpResponseMessage login = await host.LoginAsync(Token);
        login.EnsureSuccessStatusCode();
        SetCookieHeaderValue issued = Assert.Single(login.Headers.GetValues("Set-Cookie").Select(value => SetCookieHeaderValue.Parse(value)),
            cookie => cookie.Name == "__Host-FactoryPulse");
        var jar = new CookieContainer();
        jar.Add(AuthenticationFixture.Address, new Cookie(issued.Name.Value!, issued.Value.Value!, "/") { Secure = true });
        await using HubConnection connection = new HubConnectionBuilder().WithUrl(
            new Uri(AuthenticationFixture.Address, "/hubs/monitoring"), options =>
            {
                options.Cookies = jar;
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => host.Host.Server.CreateHandler();
                options.Headers.Add("Cookie", jar.GetCookieHeader(AuthenticationFixture.Address));
            }).Build();
        var connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            connectionClosed.TrySetResult();
            return Task.CompletedTask;
        };
        await connection.StartAsync(Token);
        Assert.Equal(HubConnectionState.Connected, connection.State);

        using HttpResponseMessage missingCsrf = await host.Client.PostAsync("/api/auth/logout", null, Token);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        using HttpResponseMessage logout = await host.Client.PostProtectedAsync("/api/auth/logout", Token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        using HttpClient replay = host.CreateClient();
        replay.DefaultRequestHeaders.Add("Cookie", jar.GetCookieHeader(AuthenticationFixture.Address));
        using HttpResponseMessage after = await replay.GetAsync("/api/auth/me", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }
}
