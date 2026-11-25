using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactoryPulse.IntegrationTests.Monitoring;

public sealed class MachineApiFixture : IAsyncLifetime
{
    internal static readonly Guid RunningId = new("e05831e8-41d0-4ae8-8746-51f890af159e");
    internal static readonly Guid IdleId = new("579cc6cd-9dfe-4333-ac96-b521140af59a");
    internal static readonly Guid OtherId = new("c2041131-9803-4b57-83ce-fc5b36646d96");
    internal static readonly Guid UnknownId = new("500d5a19-77dc-4d72-8474-f8c7391f5512");
    internal static readonly DateTime StartUtc = new(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

    private readonly WebApplicationFactory<Program> host = new();
    private SqliteTestDatabase? database;

    internal HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        SqliteTestDatabase store = await SqliteTestDatabase.OpenAsync(token);
        database = store;

        await using (FactoryPulseDbContext writer = store.CreateContext())
        {
            var running = new Machine(RunningId, "CNC-001", "Mill A", "CNC", "Line A", StartUtc, "Primary production mill");
            running.ChangeStatus(MachineStatus.Running, StartUtc.AddMinutes(1));
            var other = new Machine(OtherId, "CNC-002", "Mill B", "CNC", "Line A", StartUtc);
            other.ChangeStatus(MachineStatus.Running, StartUtc);
            var idle = new Machine(IdleId, "CNC-003", "Mill C", "CNC", "Line B", StartUtc);
            idle.ChangeStatus(MachineStatus.Idle, StartUtc);
            writer.Machines.AddRange(idle, running, other);
            await writer.SaveChangesAsync(token);
        }

        Client = host.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Simulation:Enabled"] = "false" }))
            .ConfigureTestServices(services => services.AddScoped(_ => store.CreateContext())))
            .CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        try
        {
            await host.DisposeAsync();
        }
        finally
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
        }
    }
}
