using FactoryPulse.Application.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Persistence;

internal static class DatabaseRegistration
{
    internal static void AddFactoryPulseDatabase(this WebApplicationBuilder builder)
    {
        string contentRoot = builder.Environment.ContentRootPath;
        builder.Services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Validate(options => IsDatabaseFilePath(options.Path), "Database:Path must identify a SQLite file.")
            .ValidateOnStart();

        builder.Services.AddDbContext<FactoryPulseDbContext>((services, options) =>
        {
            string configuredPath = services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Path;
            string databasePath = Path.GetFullPath(configuredPath, contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            var connection = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
            options.UseSqlite(connection.ToString());
        });
        builder.Services.AddScoped<MachineHistoryQueries>();
        builder.Services.AddScoped<TelemetryStore>();
        builder.Services.AddTransient<TelemetryProcessor>();
        builder.Services.AddScoped<IMachineTelemetryQueries>(services => services.GetRequiredService<MachineHistoryQueries>());
        builder.Services.AddScoped<IMachineQueries, MachineQueries>();
        builder.Services.AddScoped<IAlarmManagement, AlarmManagement>();
    }

    private static bool IsDatabaseFilePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && path.IndexOfAny(Path.GetInvalidPathChars()) < 0
            && Path.GetFileName(path) is { Length: > 0 } name
            && name is not ("." or "..") && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}
