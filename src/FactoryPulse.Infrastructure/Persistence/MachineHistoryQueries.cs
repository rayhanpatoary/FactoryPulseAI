using System.Linq.Expressions;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class MachineHistoryQueries(FactoryPulseDbContext database) : IMachineTelemetryQueries
{
    private const int MaxPageSize = 100;

    private static readonly Expression<Func<MachineReading, MachineTelemetry>> ProjectTelemetry =
        reading => new MachineTelemetry(reading.MachineId, reading.TimestampUtc, reading.Temperature,
            reading.Vibration, reading.Rpm, reading.Torque, reading.ToolWear);

    public Task<MachineTelemetry?> GetLatestAsync(Guid machineId, CancellationToken cancellationToken) =>
        LatestFirst(ReadingsFor(machineId)).Select(ProjectTelemetry).FirstOrDefaultAsync(cancellationToken);

    public async Task<TelemetryHistoryPage> GetHistoryAsync(
        Guid machineId, TelemetryHistoryFilter filter, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(filter.Page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.Page, TelemetryHistoryFilter.MaxPageNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(filter.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.PageSize, TelemetryHistoryFilter.MaxPageSize);

        IQueryable<MachineReading> readings = ReadingsFor(machineId);
        if (filter.FromUtc is DateTime from)
        {
            readings = readings.Where(reading => reading.TimestampUtc >= from);
        }

        if (filter.ToUtc is DateTime to)
        {
            readings = readings.Where(reading => reading.TimestampUtc < to);
        }

        int offset = (filter.Page - 1) * filter.PageSize;
        // Fetch one extra row to determine if another page exists without a count query.
        List<MachineTelemetry> items = await LatestFirst(readings)
            .Skip(offset).Take(filter.PageSize + 1)
            .Select(ProjectTelemetry).ToListAsync(cancellationToken);
        bool hasMore = items.Count > filter.PageSize;
        if (hasMore)
        {
            items.RemoveAt(filter.PageSize);
        }

        return new TelemetryHistoryPage(filter.Page, filter.PageSize, hasMore, items);
    }

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

        List<MachineReading> readings = await LatestFirst(ReadingsFor(machineId))
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

    private IQueryable<MachineReading> ReadingsFor(Guid machineId) =>
        database.MachineReadings.AsNoTracking().Where(reading => reading.MachineId == machineId);

    private static IOrderedQueryable<MachineReading> LatestFirst(IQueryable<MachineReading> readings) =>
        readings.OrderByDescending(reading => reading.TimestampUtc).ThenByDescending(reading => reading.Id);
}
