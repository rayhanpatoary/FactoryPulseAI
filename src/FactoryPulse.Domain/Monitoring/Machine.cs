namespace FactoryPulse.Domain.Monitoring;

public sealed class Machine
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 100;
    public const int TypeMaxLength = 50;
    public const int LocationMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private Machine() { }

    public Machine(string machineCode, string name, string machineType, string location, DateTime createdAtUtc, string? description = null)
    {
        string code = MonitoringValidation.RequiredText(machineCode, CodeMaxLength);
        if (code.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new ArgumentException("Use ASCII letters, digits, hyphens, or underscores for the machine code.", nameof(machineCode));
        }

        MonitoringValidation.RequireUtc(createdAtUtc);
        Id = Guid.NewGuid();
        MachineCode = code.ToUpperInvariant();
        Name = MonitoringValidation.RequiredText(name, NameMaxLength);
        MachineType = MonitoringValidation.RequiredText(machineType, TypeMaxLength);
        Location = MonitoringValidation.RequiredText(location, LocationMaxLength);
        Description = description is null ? null : MonitoringValidation.RequiredText(description, DescriptionMaxLength);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string MachineCode { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string MachineType { get; private set; } = null!;
    public string Location { get; private set; } = null!;
    public MachineStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeStatus(MachineStatus status, DateTime changedAtUtc)
    {
        MonitoringValidation.RequireUtc(changedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(changedAtUtc, UpdatedAtUtc);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        UpdatedAtUtc = changedAtUtc;
    }
}
