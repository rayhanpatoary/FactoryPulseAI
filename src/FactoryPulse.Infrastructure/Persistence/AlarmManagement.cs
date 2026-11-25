using System.Linq.Expressions;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class AlarmManagement(FactoryPulseDbContext database, TimeProvider clock) : IAlarmManagement
{
    private static readonly Expression<Func<MachineAlarm, AlarmDetails>> Project =
        alarm => new AlarmDetails(alarm.Id, alarm.MachineId, alarm.AlarmCode, alarm.Message, alarm.Severity,
            alarm.ResolvedAtUtc != null ? AlarmLifecycleState.Resolved
                : alarm.AcknowledgedAtUtc != null ? AlarmLifecycleState.ActiveAcknowledged : AlarmLifecycleState.ActiveUnacknowledged,
            alarm.CreatedAtUtc, alarm.AcknowledgedAtUtc, alarm.ResolvedAtUtc);

    public async Task<AlarmPage> GetPageAsync(AlarmFilter filter, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(filter.Page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.Page, AlarmFilter.MaxPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(filter.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(filter.PageSize, AlarmFilter.MaxPageSize);

        IQueryable<MachineAlarm> alarms = database.MachineAlarms.AsNoTracking();
        if (filter.MachineId is Guid machineId)
        {
            alarms = alarms.Where(alarm => alarm.MachineId == machineId);
        }

        if (filter.Severity is AlarmSeverity severity)
        {
            alarms = alarms.Where(alarm => alarm.Severity == severity);
        }

        if (filter.Code is string code)
        {
            alarms = alarms.Where(alarm => alarm.AlarmCode == code);
        }

        if (filter.Active is bool active)
        {
            alarms = active
                ? alarms.Where(alarm => alarm.ResolvedAtUtc == null)
                : alarms.Where(alarm => alarm.ResolvedAtUtc != null);
        }

        int offset = (filter.Page - 1) * filter.PageSize;
        List<AlarmDetails> items = await alarms.OrderByDescending(alarm => alarm.CreatedAtUtc).ThenByDescending(alarm => alarm.Id)
            .Skip(offset).Take(filter.PageSize + 1).Select(Project).ToListAsync(cancellationToken);
        bool hasMore = items.Count > filter.PageSize;
        if (hasMore)
        {
            items.RemoveAt(filter.PageSize);
        }

        return new AlarmPage(filter.Page, filter.PageSize, hasMore, items);
    }

    public async Task<AlarmDetails?> AcknowledgeAsync(long id, CancellationToken cancellationToken)
    {
        DateTime utcNow = clock.GetUtcNow().UtcDateTime;
        await database.MachineAlarms.Where(alarm => alarm.Id == id && alarm.AcknowledgedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(alarm => alarm.AcknowledgedAtUtc,
                alarm => (DateTime?)(alarm.CreatedAtUtc > utcNow ? alarm.CreatedAtUtc : utcNow)), cancellationToken);

        return await database.MachineAlarms.AsNoTracking().Where(alarm => alarm.Id == id)
            .Select(Project).SingleOrDefaultAsync(cancellationToken);
    }
}
