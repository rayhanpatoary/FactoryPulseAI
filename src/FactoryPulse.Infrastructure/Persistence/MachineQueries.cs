using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class MachineQueries(FactoryPulseDbContext database) : IMachineQueries
{
    public async Task<IReadOnlyList<MachineListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return await database.Machines.AsNoTracking()
            .OrderBy(machine => machine.MachineCode)
            .Select(machine => new MachineListItem(machine.Id, machine.MachineCode, machine.Name, machine.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<MachineDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return database.Machines.AsNoTracking()
            .Where(machine => machine.Id == id)
            .Select(machine => new MachineDetails(machine.Id, machine.MachineCode, machine.Name, machine.Description,
                machine.MachineType, machine.Location, machine.Status, machine.CreatedAtUtc, machine.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        database.Machines.AsNoTracking().AnyAsync(machine => machine.Id == id, cancellationToken);

    public async Task<MachineSummary> SummarizeAsync(CancellationToken cancellationToken)
    {
        Dictionary<MachineStatus, long> counts = await database.Machines.AsNoTracking()
            .GroupBy(machine => machine.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);

        return new MachineSummary(counts.Values.Sum(), counts.GetValueOrDefault(MachineStatus.Offline),
            counts.GetValueOrDefault(MachineStatus.Running), counts.GetValueOrDefault(MachineStatus.Idle),
            counts.GetValueOrDefault(MachineStatus.Faulted));
    }
}
