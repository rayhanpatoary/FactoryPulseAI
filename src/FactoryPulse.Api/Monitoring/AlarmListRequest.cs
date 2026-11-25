using System.ComponentModel.DataAnnotations;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Api.Monitoring;

public sealed class AlarmListRequest
{
    [Range(1, AlarmFilter.MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, AlarmFilter.MaxPageSize)]
    public int PageSize { get; init; } = AlarmFilter.DefaultPageSize;

    public AlarmSeverity? Severity { get; init; }

    [StringLength(MachineAlarm.CodeMaxLength)]
    public string? Code { get; init; }

    public bool? Active { get; init; }

    internal AlarmFilter ToFilter(Guid? machineId, bool activeOnly = false) =>
        new(Page, PageSize, machineId, Severity, Code, activeOnly ? true : Active);
}
