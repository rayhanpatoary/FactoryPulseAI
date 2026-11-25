using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactoryPulse.Infrastructure.Persistence;

public readonly record struct AlarmResolution(long Id, DateTime TimestampUtc);

public sealed class TelemetryStore(FactoryPulseDbContext database)
{
    public Task<List<MachineAlarm>> LoadActiveAsync(Guid[] machineIds, CancellationToken cancellationToken) =>
        database.MachineAlarms.AsNoTracking()
            .Where(alarm => machineIds.Contains(alarm.MachineId) && alarm.ResolvedAtUtc == null
                && (alarm.AlarmCode == AlarmCodes.TemperatureWarning || alarm.AlarmCode == AlarmCodes.TemperatureCritical
                    || alarm.AlarmCode == AlarmCodes.VibrationWarning))
            .ToListAsync(cancellationToken);

    public async Task SaveBatchAsync(
        IReadOnlyList<MachineReading> readings, IReadOnlyList<MachineAlarm> opened,
        IReadOnlyList<AlarmResolution> resolutions, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = resolutions.Count == 0
            ? null
            : await database.Database.BeginTransactionAsync(cancellationToken);

        foreach (AlarmResolution resolution in resolutions)
        {
            // Update only the resolution column so concurrent acknowledgement is preserved.
            int updated = await database.MachineAlarms
                .Where(alarm => alarm.Id == resolution.Id && alarm.ResolvedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(alarm => alarm.ResolvedAtUtc, resolution.TimestampUtc),
                    cancellationToken);
            if (updated != 1)
            {
                throw new DbUpdateConcurrencyException("The active alarm changed during telemetry processing.");
            }
        }

        database.MachineReadings.AddRange(readings);
        database.MachineAlarms.AddRange(opened);
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
