using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public sealed class FactoryPulseDbContext(DbContextOptions<FactoryPulseDbContext> options)
    : IdentityDbContext<MonitoringUser>(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineReading> MachineReadings => Set<MachineReading>();
    public DbSet<MachineAlarm> MachineAlarms => Set<MachineAlarm>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcTimestampConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new MachineConfiguration());
        builder.ApplyConfiguration(new MachineReadingConfiguration());
        builder.ApplyConfiguration(new MachineAlarmConfiguration());
    }
}
