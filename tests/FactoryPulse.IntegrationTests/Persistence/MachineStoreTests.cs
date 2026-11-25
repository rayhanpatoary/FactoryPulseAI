using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class MachineStoreTests
{
    private static readonly DateTime SampleTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MachineAndReadingRoundTripThroughMigratedSqlite()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        var machine = new Machine("MILL-01", "Mill", "CNC", "Hall A", SampleTimeUtc);
        DateTime readingTimeUtc = SampleTimeUtc.AddSeconds(10);
        var reading = new MachineReading(machine.Id, readingTimeUtc, 42.5, 0.25, 1000, 12, 0.25);

        await using (FactoryPulseDbContext writer = database.CreateContext())
        {
            writer.MachineReadings.Add(reading);
            writer.Machines.Add(machine);
            await writer.SaveChangesAsync(token);
        }

        await using FactoryPulseDbContext reader = database.CreateContext();
        Machine savedMachine = await reader.Machines.AsNoTracking().SingleAsync(item => item.Id == machine.Id, token);
        MachineReading savedReading = await reader.MachineReadings.AsNoTracking()
            .SingleAsync(item => item.MachineId == savedMachine.Id && item.TimestampUtc >= readingTimeUtc, token);
        Assert.Equal("MILL-01", savedMachine.MachineCode);
        Assert.Equal(DateTimeKind.Utc, savedMachine.CreatedAtUtc.Kind);
        Assert.True(savedReading.Id > 0);
        Assert.Equal(reading.Id, savedReading.Id);
        Assert.Equal(readingTimeUtc, savedReading.TimestampUtc);
        Assert.Equal(DateTimeKind.Utc, savedReading.TimestampUtc.Kind);
        Assert.Equal(42.5, savedReading.Temperature);
    }

    [Fact]
    public async Task SqliteRejectsDuplicateCanonicalMachineCodes()
    {
        const int UniqueConstraintViolation = 2067;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token);
        await using FactoryPulseDbContext context = database.CreateContext();
        context.Machines.Add(new Machine("MILL-01", "First", "CNC", "Hall A", SampleTimeUtc));
        await context.SaveChangesAsync(token);

        context.Machines.Add(new Machine(" mill-01 ", "Second", "CNC", "Hall B", SampleTimeUtc));
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(token));

        Assert.Equal(UniqueConstraintViolation, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        Assert.Equal(1, await context.Machines.CountAsync(token));
    }
}
