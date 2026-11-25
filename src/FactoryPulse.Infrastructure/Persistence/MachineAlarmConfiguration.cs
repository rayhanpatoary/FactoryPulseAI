using FactoryPulse.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryPulse.Infrastructure.Persistence;

internal sealed class MachineAlarmConfiguration : IEntityTypeConfiguration<MachineAlarm>
{
    public void Configure(EntityTypeBuilder<MachineAlarm> builder)
    {
        builder.ToTable("MachineAlarms", table =>
        {
            table.HasCheckConstraint("CK_MachineAlarms_Text",
                $"""length(trim("AlarmCode")) BETWEEN 1 AND {MachineAlarm.CodeMaxLength} AND length(trim("Message")) BETWEEN 1 AND {MachineAlarm.MessageMaxLength}""");
            table.HasCheckConstraint("CK_MachineAlarms_Severity", "\"Severity\" IN (0, 1, 2)");
            table.HasCheckConstraint("CK_MachineAlarms_AcknowledgementOrder",
                "\"AcknowledgedAtUtc\" IS NULL OR \"AcknowledgedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.HasKey(alarm => alarm.Id);
        builder.Property(alarm => alarm.AlarmCode).HasMaxLength(MachineAlarm.CodeMaxLength).IsRequired();
        builder.Property(alarm => alarm.Message).HasMaxLength(MachineAlarm.MessageMaxLength).IsRequired();
        builder.Property(alarm => alarm.AcknowledgedAtUtc).IsConcurrencyToken();
        builder.Ignore(alarm => alarm.IsAcknowledged);
        builder.HasOne<Machine>().WithMany()
            .HasForeignKey(alarm => alarm.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(alarm => new { alarm.MachineId, alarm.AcknowledgedAtUtc, alarm.CreatedAtUtc });
    }
}
