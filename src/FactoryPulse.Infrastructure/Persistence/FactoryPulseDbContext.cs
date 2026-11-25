using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class FactoryPulseDbContext(DbContextOptions<FactoryPulseDbContext> options) : DbContext(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineReading> MachineReadings => Set<MachineReading>();
    public DbSet<MachineAlarm> MachineAlarms => Set<MachineAlarm>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcTimestampConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MachineConfiguration());
        modelBuilder.ApplyConfiguration(new MachineReadingConfiguration());
        modelBuilder.ApplyConfiguration(new MachineAlarmConfiguration());
    }
}
