using FactoryPulse.Application.Monitoring;
using FactoryPulse.Infrastructure.Simulation;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Simulation;

internal static class SimulationRegistration
{
    internal static void AddFactoryPulseSimulation(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<SimulationOptions>()
            .BindConfiguration(SimulationOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SimulationOptions>, SimulationOptionsValidator>();
        builder.Services.AddSingleton<IMachineDataSource, SimulatedMachineDataSource>();
        builder.Services.AddScoped<SimulationStore>();
        builder.Services.AddHostedService<SimulationWorker>();
    }
}
