using System.Net;
using System.Net.Http.Json;
using FactoryPulse.Api.Controllers;
using FactoryPulse.Api.Security;
using Xunit;

namespace FactoryPulse.IntegrationTests.Authentication;

public sealed class AccountActivationTests
{
    [Theory]
    [InlineData(MonitoringAccess.Operator, HttpStatusCode.Forbidden)]
    [InlineData(MonitoringAccess.Administrator, HttpStatusCode.NoContent)]
    public async Task OnlyAnAdministratorCanDeactivateAnotherAccount(string role, HttpStatusCode expectedStatus)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using AuthenticationFixture host = await AuthenticationFixture.CreateAsync(token);
        await host.CreateUserAsync(role, token);
        await host.CreateUserAsync(MonitoringAccess.Viewer, token, "target-user");
        using HttpClient target = host.CreateClient();
        using HttpResponseMessage targetLogin = await target.PostProtectedAsync("/api/auth/login", token,
            JsonContent.Create(new LoginRequest("target-user", host.Password)));
        targetLogin.EnsureSuccessStatusCode();
        using HttpResponseMessage login = await host.LoginAsync(token);
        login.EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/admin/users/target-user/activation")
        {
            Content = JsonContent.Create(new AccountActivationRequest(false))
        };
        request.Headers.Add("X-XSRF-TOKEN", await TestSession.GetRequestTokenAsync(host.Client, token));
        using HttpResponseMessage result = await host.Client.SendAsync(request, token);
        Assert.Equal(expectedStatus, result.StatusCode);
        using HttpResponseMessage after = await target.GetAsync("/api/auth/me", token);
        Assert.Equal(expectedStatus == HttpStatusCode.NoContent ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, after.StatusCode);
    }
}
