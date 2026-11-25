using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FactoryPulse.Infrastructure.Persistence;

// SQLite can compare and index integer ticks and the converter restores DateTimeKind on reads.
internal sealed class UtcTimestampConverter : ValueConverter<DateTime, long>
{
    public UtcTimestampConverter()
        : base(value => value.Ticks, ticks => new DateTime(ticks, DateTimeKind.Utc))
    {
    }
}
