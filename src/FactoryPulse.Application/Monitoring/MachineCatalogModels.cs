using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Application.Monitoring;

public sealed record MachineListItem(Guid Id, string MachineCode, string Name, MachineStatus Status);

public sealed record MachineDetails(
    Guid Id, string MachineCode, string Name, string? Description, string MachineType, string Location,
    MachineStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record MachineSummary(long Total, long Offline, long Running, long Idle, long Faulted);
