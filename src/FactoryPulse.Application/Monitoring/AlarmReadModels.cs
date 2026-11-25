using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Application.Monitoring;

public enum AlarmLifecycleState
{
    ActiveUnacknowledged,
    ActiveAcknowledged,
    Resolved
}

public sealed record AlarmDetails(long Id, Guid MachineId, string AlarmCode, string Message,
    AlarmSeverity Severity, AlarmLifecycleState State, DateTime CreatedAtUtc, DateTime? AcknowledgedAtUtc, DateTime? ResolvedAtUtc);

public sealed record AlarmFilter(int Page, int PageSize, Guid? MachineId, AlarmSeverity? Severity, string? Code, bool? Active)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;
}

public sealed record AlarmPage(int Page, int PageSize, bool HasMore, IReadOnlyList<AlarmDetails> Items);
