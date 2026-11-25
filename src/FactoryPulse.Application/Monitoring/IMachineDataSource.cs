using FactoryPulse.Domain.Monitoring;

namespace FactoryPulse.Application.Monitoring;

public interface IMachineDataSource
{
    ValueTask<MachineReading> ReadAsync(Machine machine, CancellationToken cancellationToken);
}
