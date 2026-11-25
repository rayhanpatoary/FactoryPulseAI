using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class TelemetryStoreTests
{
    [Fact]
    public async Task FailedAlarmInsertRollsBackResolutionAndReading()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTime now = DateTime.UnixEpoch;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var machine = new Machine("ATOMIC-01", "Mill", "CNC", "Line A", now);
        var warning = new MachineAlarm(machine.Id, AlarmCodes.TemperatureWarning, "Temperature warning", AlarmSeverity.Warning, now);
        var vibration = new MachineAlarm(machine.Id, AlarmCodes.VibrationWarning, "Vibration warning", AlarmSeverity.Warning, now);
        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.AddRange(machine, warning, vibration);
            await writer.SaveChangesAsync(token);
        }

        DateTime next = now.AddSeconds(1);
        var reading = new MachineReading(machine.Id, next, 70, 6, 2400, 18, 0.1);
        var duplicate = new MachineAlarm(machine.Id, AlarmCodes.VibrationWarning, "Vibration warning", AlarmSeverity.Warning, next);
        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            var telemetry = new TelemetryStore(writer);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                telemetry.SaveBatchAsync([reading], [duplicate], [new AlarmResolution(warning.Id, next)], token));
        }

        await using FactoryPulseDbContext reader = database.CreateContext();
        Assert.Equal(2, (await new TelemetryStore(reader).LoadActiveAsync([machine.Id], token)).Count);
        Assert.False(await reader.MachineReadings.AnyAsync(token));
    }
}
