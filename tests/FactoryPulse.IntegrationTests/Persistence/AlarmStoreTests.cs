using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class AlarmStoreTests
{
    private static readonly DateTime SampleTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AlarmAcknowledgementPersistsAndRejectsStaleUpdates()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var machine = new Machine("MILL-01", "Mill", "CNC", "Hall A", SampleTimeUtc);
        var alarm = new MachineAlarm(machine.Id, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, SampleTimeUtc);
        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.Machines.Add(machine);
            writer.MachineAlarms.Add(alarm);
            await writer.SaveChangesAsync(token);
        }

        await using FactoryPulseDbContext firstWriter = database.CreateContext();
        await using FactoryPulseDbContext staleWriter = database.CreateContext();
        MachineAlarm first = await firstWriter.MachineAlarms.SingleAsync(item => item.MachineId == machine.Id, token);
        MachineAlarm stale = await staleWriter.MachineAlarms.SingleAsync(item => item.Id == alarm.Id, token);
        Assert.Equal("OVERHEAT", first.AlarmCode);
        Assert.Equal(AlarmSeverity.Warning, first.Severity);
        Assert.False(first.IsAcknowledged);

        DateTime acknowledgedAtUtc = SampleTimeUtc.AddMinutes(1);
        first.Acknowledge(acknowledgedAtUtc);
        stale.Acknowledge(acknowledgedAtUtc.AddMinutes(1));
        await firstWriter.SaveChangesAsync(token);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleWriter.SaveChangesAsync(token));

        await using FactoryPulseDbContext reader = database.CreateContext();
        MachineAlarm saved = await reader.MachineAlarms.AsNoTracking().SingleAsync(item => item.Id == alarm.Id, token);
        Assert.Equal(machine.Id, saved.MachineId);
        Assert.True(saved.IsAcknowledged);
        Assert.Equal(acknowledgedAtUtc, saved.AcknowledgedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.AcknowledgedAtUtc?.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SqliteRejectsMonitoringRecordsForMissingMachines(bool insertAlarm)
    {
        const int ForeignKeyConstraintViolation = 787;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        await using FactoryPulseDbContext context = database.CreateContext();
        var missingMachineId = new Guid("93d2c76f-f0db-4460-afce-f696ea0a3825");

        if (insertAlarm)
        {
            context.MachineAlarms.Add(new MachineAlarm(missingMachineId, "OVERHEAT", "TEMP_HIGH", AlarmSeverity.Warning, SampleTimeUtc));
        }
        else
        {
            context.MachineReadings.Add(new MachineReading(missingMachineId, SampleTimeUtc, 40, 0.1, 500, 8, 0.2));
        }

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(token));

        Assert.Equal(ForeignKeyConstraintViolation, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
    }
}
