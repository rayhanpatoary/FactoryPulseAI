using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class MachineHistoryQueries(FactoryPulseDbContext database)
{
    private const int MaxPageSize = 100;

    public async Task<MachineHistoryPage?> GetPageAsync(
        Guid machineId, int readingOffset, int alarmOffset, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(readingOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(alarmOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        Machine? machine = await database.Machines.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == machineId, cancellationToken);
        if (machine is null)
        {
            return null;
        }

        List<MachineReading> readings = await database.MachineReadings.AsNoTracking()
            .Where(reading => reading.MachineId == machineId)
            .OrderByDescending(reading => reading.TimestampUtc)
            .ThenByDescending(reading => reading.Id)
            .Skip(readingOffset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        List<MachineAlarm> openAlarms = await database.MachineAlarms.AsNoTracking()
            .Where(alarm => alarm.MachineId == machineId && alarm.AcknowledgedAtUtc == null)
            .OrderByDescending(alarm => alarm.CreatedAtUtc)
            .ThenByDescending(alarm => alarm.Id)
            .Skip(alarmOffset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MachineHistoryPage(machine, readings, openAlarms);
    }
}
