namespace FactoryPulse.Application.Monitoring;

public interface IMachineQueries
{
    Task<IReadOnlyList<MachineListItem>> ListAsync(CancellationToken cancellationToken);
    Task<MachineDetails?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<MachineSummary> SummarizeAsync(CancellationToken cancellationToken);
}
