using FactoryPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.IntegrationTests.Persistence;

internal sealed class SqliteTestDatabase(SqliteConnection connection) : IAsyncDisposable
{
    private readonly DbContextOptions<FactoryPulseDbContext> options =
        new DbContextOptionsBuilder<FactoryPulseDbContext>().UseSqlite(connection).Options;

    internal FactoryPulseDbContext CreateContext() => new(options);

    internal static async Task<SqliteTestDatabase> OpenAsync(CancellationToken cancellationToken, string? targetMigration = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True;Pooling=False");
        try
        {
            // SQLite holds this database only while its connection stays open.
            await connection.OpenAsync(cancellationToken);
            var database = new SqliteTestDatabase(connection);
            await using FactoryPulseDbContext context = database.CreateContext();
            await context.Database.MigrateAsync(targetMigration, cancellationToken);
            return database;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
