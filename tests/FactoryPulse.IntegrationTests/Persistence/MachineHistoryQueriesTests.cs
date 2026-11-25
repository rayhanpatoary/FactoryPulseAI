using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class MachineHistoryQueriesTests
{
    [Fact]
    public async Task HistoryPagesAreBoundedAndOrderedForTheSelectedMachine()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var machine = new Machine("MILL-01", "Mill", "CNC", "Hall A", time);
        var otherMachine = new Machine("MILL-02", "Mill", "CNC", "Hall B", time);
        var firstReading = new MachineReading(machine.Id, time, 40, 0.1, 500, 8, 0.2);
        var tiedReading = new MachineReading(machine.Id, time, 41, 0.1, 500, 8, 0.2);
        var otherReading = new MachineReading(otherMachine.Id, time.AddMinutes(1), 42, 0.1, 500, 8, 0.2);
        var openAlarm = new MachineAlarm(machine.Id, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, time);
        var newerAlarm = new MachineAlarm(machine.Id, "VIBRATION", "VIB_HIGH", AlarmSeverity.Warning, time.AddMinutes(1));
        var acknowledged = new MachineAlarm(machine.Id, "OVERSPEED", "RPM_HIGH", AlarmSeverity.Critical, time.AddMinutes(2));
        acknowledged.Acknowledge(time.AddMinutes(3));

        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.AddRange(machine, otherMachine, firstReading, tiedReading, otherReading, openAlarm, newerAlarm, acknowledged);
            await writer.SaveChangesAsync(token);
        }

        await using FactoryPulseDbContext reader = database.CreateContext();
        var queries = new MachineHistoryQueries(reader);
        MachineHistoryPage? first = await queries.GetPageAsync(machine.Id, 0, 0, 1, token);
        MachineHistoryPage? second = await queries.GetPageAsync(machine.Id, 1, 1, 1, token);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(machine.Id, first.Machine.Id);
        Assert.Equal(Math.Max(firstReading.Id, tiedReading.Id), Assert.Single(first.Readings).Id);
        Assert.Equal(Math.Min(firstReading.Id, tiedReading.Id), Assert.Single(second.Readings).Id);
        Assert.Equal(newerAlarm.Id, Assert.Single(first.OpenAlarms).Id);
        Assert.Equal(openAlarm.Id, Assert.Single(second.OpenAlarms).Id);
        Assert.Empty(reader.ChangeTracker.Entries());
    }
}
