using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class SimulationStoreTests
{
    [Fact]
    public async Task InitializationReusesRowsAndPersistsReadingsAgainstTheirActualIds()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTime now = DateTime.UnixEpoch;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var existing = new Machine("CNC-001", "Existing CNC", "CNC", "Line A", now);
        var reused = new SimulationProfile { Id = new Guid("44626e45-70b8-4e3c-9352-2be789ac41ba"), Code = existing.MachineCode, Status = MachineStatus.Running };
        var created = new SimulationProfile { Id = new Guid("3d4adac7-d22b-437a-84be-c219385f67f9"), Code = "CNC-002", Status = MachineStatus.Idle };

        await using (FactoryPulseDbContext initial = database.CreateContext())
        {
            initial.Machines.Add(existing);
            await initial.SaveChangesAsync(token);
            await new SimulationStore(initial).InitializeAsync([reused, created], now, token);
        }

        await using FactoryPulseDbContext restarted = database.CreateContext();
        var store = new SimulationStore(restarted);
        Machine[] machines = await store.InitializeAsync([reused, created], now, token);
        await store.SaveReadingsAsync(
            [new MachineReading(machines[0].Id, now.AddSeconds(1), 25, 1, 1400, 15, 0.01)], token);

        Assert.Equal(2, await restarted.Machines.CountAsync(token));
        Assert.Equal(existing.Id, machines[0].Id);
        Assert.Equal("Existing CNC", machines[0].Name);
        Assert.Equal(MachineStatus.Running, machines[0].Status);
        Assert.Equal(created.Id, machines[1].Id);
        Assert.Equal(MachineStatus.Idle, machines[1].Status);
        MachineReading reading = await restarted.MachineReadings.AsNoTracking().SingleAsync(token);
        Assert.Equal(existing.Id, reading.MachineId);
    }
}
