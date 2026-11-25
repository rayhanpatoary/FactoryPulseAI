using FactoryPulse.Application.Monitoring;
using FactoryPulse.Domain.Monitoring;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Simulation;

internal sealed partial class SimulationWorker(
    IOptions<SimulationOptions> options,
    IMachineDataSource source,
    IServiceScopeFactory scopeFactory,
    TelemetryProcessor telemetry,
    TimeProvider clock,
    ILogger<SimulationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SimulationOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        string operation = "machine initialization";
        try
        {
            Machine[] machines;
            await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
            {
                machines = await scope.ServiceProvider.GetRequiredService<SimulationStore>()
                    .InitializeAsync(settings.Profiles, clock.GetUtcNow().UtcDateTime, stoppingToken);
            }

            await telemetry.InitializeAsync([.. machines.Select(machine => machine.Id)], stoppingToken);
            LogInitialized(logger, machines.Length);
            long lastPersistedAt = clock.GetTimestamp();
            using var timer = new PeriodicTimer(settings.SamplingInterval, clock);
            var latestReadings = new MachineReading[machines.Length];
            LogStarted(logger, settings.SamplingInterval, settings.PersistenceInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                operation = "telemetry sampling";
                long sampledAt = clock.GetTimestamp();
                for (int index = 0; index < machines.Length; index++)
                {
                    latestReadings[index] = await source.ReadAsync(machines[index], stoppingToken);
                }

                operation = "telemetry evaluation and persistence";
                bool persistenceDue = clock.GetElapsedTime(lastPersistedAt, sampledAt) >= settings.PersistenceInterval;
                if (await telemetry.ProcessAsync(latestReadings, persistenceDue, stoppingToken))
                {
                    lastPersistedAt = sampledAt;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            LogFailed(logger, operation, exception);
            throw;
        }
        finally
        {
            LogStopped(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Machine simulation initialized with {MachineCount} profiles.")]
    private static partial void LogInitialized(ILogger logger, int machineCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Machine simulation started. Sampling every {SamplingInterval} and persisting every {PersistenceInterval}.")]
    private static partial void LogStarted(ILogger logger, TimeSpan samplingInterval, TimeSpan persistenceInterval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Machine simulation failed during {Operation}.")]
    private static partial void LogFailed(ILogger logger, string operation, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Machine simulation stopped.")]
    private static partial void LogStopped(ILogger logger);
}
