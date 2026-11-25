using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryPulse.Infrastructure.Persistence;

internal sealed class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines", table =>
        {
            table.HasCheckConstraint("CK_Machines_Code",
                $"""length("MachineCode") BETWEEN 1 AND {Machine.CodeMaxLength} AND "MachineCode" NOT GLOB '*[^A-Z0-9_-]*'""");
            table.HasCheckConstraint("CK_Machines_Metadata",
                $"""length(trim("Name")) BETWEEN 1 AND {Machine.NameMaxLength} AND length(trim("MachineType")) BETWEEN 1 AND {Machine.TypeMaxLength} AND length(trim("Location")) BETWEEN 1 AND {Machine.LocationMaxLength} AND ("Description" IS NULL OR length(trim("Description")) BETWEEN 1 AND {Machine.DescriptionMaxLength})""");
            table.HasCheckConstraint("CK_Machines_Status", "\"Status\" IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_Machines_TimestampOrder", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.HasKey(machine => machine.Id);
        builder.Property(machine => machine.Id).ValueGeneratedNever();
        builder.Property(machine => machine.MachineCode).HasMaxLength(Machine.CodeMaxLength).IsRequired();
        builder.Property(machine => machine.Name).HasMaxLength(Machine.NameMaxLength).IsRequired();
        builder.Property(machine => machine.Description).HasMaxLength(Machine.DescriptionMaxLength);
        builder.Property(machine => machine.MachineType).HasMaxLength(Machine.TypeMaxLength).IsRequired();
        builder.Property(machine => machine.Location).HasMaxLength(Machine.LocationMaxLength).IsRequired();
        builder.HasIndex(machine => machine.MachineCode).IsUnique();
    }
}
