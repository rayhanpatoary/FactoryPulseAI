namespace FactoryPulse.Api.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Path { get; init; } = string.Empty;
}
