using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Simulation;

public sealed class SimulationStore(FactoryPulseDbContext database)
{
    public async Task<Machine[]> InitializeAsync(
        IReadOnlyList<SimulationProfile> profiles, DateTime nowUtc, CancellationToken cancellationToken)
    {
        string[] codes = [.. profiles.Select(profile => profile.Code)];
        Dictionary<string, Machine> existing = await database.Machines
            .Where(machine => codes.Contains(machine.MachineCode))
            .ToDictionaryAsync(machine => machine.MachineCode, StringComparer.Ordinal, cancellationToken);
        var machines = new Machine[profiles.Count];

        for (int index = 0; index < profiles.Count; index++)
        {
            SimulationProfile profile = profiles[index];
            if (!existing.TryGetValue(profile.Code, out Machine? machine))
            {
                machine = new Machine(profile.Id, profile.Code, profile.Code, "CNC", "Simulation", nowUtc);
                database.Machines.Add(machine);
            }

            if (machine.Status != profile.Status)
            {
                machine.ChangeStatus(profile.Status, nowUtc < machine.UpdatedAtUtc ? machine.UpdatedAtUtc : nowUtc);
            }

            machines[index] = machine;
        }

        await database.SaveChangesAsync(cancellationToken);
        return machines;
    }
}
