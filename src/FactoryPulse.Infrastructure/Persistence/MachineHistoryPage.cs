using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed record MachineHistoryPage(
    Machine Machine,
    IReadOnlyList<MachineReading> Readings,
    IReadOnlyList<MachineAlarm> OpenAlarms);
