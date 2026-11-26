using System.Net.Http.Json;
using System.Security.Cryptography;
using FactoryPulse.Api.Controllers;
using FactoryPulse.Api.Security;
using FactoryPulse.Infrastructure.Identity;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Authentication;

internal sealed class AuthenticationFixture : IAsyncDisposable
{
    internal static readonly Uri Address = new("https://localhost");
    private readonly SqliteTestDatabase database;
    private readonly WebApplicationFactory<Program> factory = new();

    private AuthenticationFixture(SqliteTestDatabase database, bool allowProduction)
    {
        this.database = database;
        Host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealtimeMonitoring:Enabled"] = "true",
                ["RealtimeMonitoring:AllowProduction"] = allowProduction.ToString(),
                ["Simulation:Enabled"] = "false"
            }))
            .ConfigureTestServices(services =>
            {
                services.AddScoped(_ => database.CreateContext());
                services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = Clock);
            }));
        Client = CreateClient();
    }

    internal static async Task<AuthenticationFixture> CreateAsync(CancellationToken token, bool allowProduction = false) =>
        new(await SqliteTestDatabase.OpenAsync(token), allowProduction);

    internal WebApplicationFactory<Program> Host { get; }
    internal HttpClient Client { get; }
    internal TestClock Clock { get; } = new();
    internal string Password { get; } = $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}a!1";

    internal HttpClient CreateClient() => Host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = Address,
        AllowAutoRedirect = false
    });

    internal async Task CreateUserAsync(string role, CancellationToken token)
    {
        await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioning>();
        IdentityResult result = await provisioner.CreateAsync("test-user", role, Password, token);
        Assert.True(result.Succeeded);
    }

    internal async Task DisableUserAsync()
    {
        await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MonitoringUser>>();
        MonitoringUser? user = await users.FindByNameAsync("test-user");
        Assert.NotNull(user);
        user.IsActive = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    internal Task<HttpResponseMessage> LoginAsync(CancellationToken token, string? password = null, string userName = "test-user") =>
        Client.PostProtectedAsync("/api/auth/login", token, JsonContent.Create(new LoginRequest(userName, password ?? Password)));

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    internal sealed class TestClock : TimeProvider
    {
        private long offsetTicks;

        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow().AddTicks(Interlocked.Read(ref offsetTicks));

        internal void Advance(TimeSpan interval) => Interlocked.Add(ref offsetTicks, interval.Ticks);
    }
}
