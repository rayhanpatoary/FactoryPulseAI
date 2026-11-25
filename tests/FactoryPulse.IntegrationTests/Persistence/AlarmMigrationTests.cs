using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryPulse.IntegrationTests.Persistence;

public sealed class AlarmMigrationTests
{
    [Fact]
    public async Task UpgradePreservesDuplicateLegacyRowsAsHistoryAndEnforcesOneActiveAlarm()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string Initial = "20261125113255_InitialCreate";
        const string Code = "LEGACY_WARNING";
        const string Message = "Legacy sensor warning";
        DateTime created = DateTime.UnixEpoch;
        DateTime newest = created.AddMinutes(1);
        DateTime acknowledged = created.AddMinutes(2);
        await using SqliteTestDatabase database = await SqliteTestDatabase.OpenAsync(token, Initial);
        await using FactoryPulseDbContext context = database.CreateContext();
        var machine = new Machine("LEGACY-01", "Legacy mill", "CNC", "Line A", created);
        context.Machines.Add(machine);
        await context.SaveChangesAsync(token);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MachineAlarms" ("MachineId", "AlarmCode", "Message", "Severity", "CreatedAtUtc", "AcknowledgedAtUtc")
            VALUES ({machine.Id}, {Code}, {Message}, {1}, {created.Ticks}, {acknowledged.Ticks}),
                   ({machine.Id}, {Code}, {Message}, {1}, {newest.Ticks}, NULL)
            """, token);

        await context.Database.MigrateAsync(token);

        List<MachineAlarm> alarms = await context.MachineAlarms.AsNoTracking().OrderBy(alarm => alarm.Id).ToListAsync(token);
        Assert.Equal(2, alarms.Count);
        Assert.Equal(acknowledged, alarms[0].AcknowledgedAtUtc);
        Assert.Equal(acknowledged, alarms[0].ResolvedAtUtc);
        Assert.True(alarms[1].IsActive);
        context.MachineAlarms.Add(new MachineAlarm(machine.Id, Code, Message, AlarmSeverity.Warning, newest));
        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(token));
        Assert.Equal(2067, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);

        await context.Database.MigrateAsync(Initial, token);
        Assert.Equal(Initial, Assert.Single(await context.Database.GetAppliedMigrationsAsync(token)));
    }
}
