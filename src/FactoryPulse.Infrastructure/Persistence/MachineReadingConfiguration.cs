using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryPulse.Infrastructure.Persistence;

internal sealed class MachineReadingConfiguration : IEntityTypeConfiguration<MachineReading>
{
    public void Configure(EntityTypeBuilder<MachineReading> builder)
    {
        builder.ToTable("MachineReadings", table => table.HasCheckConstraint(
            "CK_MachineReadings_MeasurementRange", "\"Vibration\" >= 0 AND \"Rpm\" >= 0 AND \"ToolWear\" BETWEEN 0 AND 1"));
        builder.HasKey(reading => reading.Id);
        builder.HasOne<Machine>().WithMany()
            .HasForeignKey(reading => reading.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(reading => new { reading.MachineId, reading.TimestampUtc });
    }
}
