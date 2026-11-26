using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryPulse.Api.Persistence;
using FactoryPulse.Api.Realtime;
using FactoryPulse.Api.Security;
using FactoryPulse.Api.Simulation;
using FactoryPulse.Application.Monitoring;
using FactoryPulse.Application.Status;
using FactoryPulse.Domain.Monitoring;
using Microsoft.Extensions.Options;

bool provisionUser = args is ["provision-user", ..];
WebApplicationBuilder builder = WebApplication.CreateBuilder(provisionUser ? [] : args);

builder.Services.AddControllersWithViews().AddJsonOptions(options => ConfigureMonitoringJson(options.JsonSerializerOptions));
builder.Services.AddSignalR().AddJsonProtocol(options => ConfigureMonitoringJson(options.PayloadSerializerOptions));
builder.Services.AddOptions<RealtimeMonitoringOptions>().BindConfiguration(RealtimeMonitoringOptions.SectionName).ValidateOnStart();
builder.Services.AddSingleton<IRealtimeMonitoringPublisher, SignalRMonitoringPublisher>();
builder.Services.AddProblemDetails();
builder.Services.AddMonitoringIdentity();
builder.Services.AddSingleton<ActiveMonitoringSessions>();
builder.Services.AddScoped<AccountProvisioning>();
builder.Services.AddHealthChecks();
builder.AddFactoryPulseDatabase();
builder.AddFactoryPulseSimulation();

builder.Services.AddOptions<AlarmDetectionOptions>()
    .BindConfiguration(AlarmDetectionOptions.SectionName)
    .Validate(settings => settings.HasValidThresholds(), "AlarmDetection thresholds must be finite and ordered, with clear limits below their triggers.")
    .ValidateOnStart();
builder.Services.AddSingleton(services =>
    new AlarmDetectionService(services.GetRequiredService<IOptions<AlarmDetectionOptions>>().Value));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

builder.Services.AddOptions<ApplicationStatusOptions>()
    .BindConfiguration(ApplicationStatusOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.InstanceName),
        "ApplicationStatus:InstanceName must contain a non-whitespace value.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(services => new ApplicationStatusService(
    services.GetRequiredService<IOptions<ApplicationStatusOptions>>().Value,
    services.GetRequiredService<TimeProvider>()));

WebApplication app = builder.Build();

if (provisionUser)
{
    Environment.ExitCode = await AccountProvisioning.RunAsync(app.Services, args);
    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
    }

    await next(context);
});
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers().RequireAuthorization(MonitoringAccess.Read);
app.MapHealthChecks("/health");
app.MapFactoryPulseMonitoringHub();

app.Run();

static void ConfigureMonitoringJson(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter<MachineStatus>(allowIntegerValues: false));
    options.Converters.Add(new JsonStringEnumConverter<AlarmSeverity>(allowIntegerValues: false));
    options.Converters.Add(new JsonStringEnumConverter<AlarmLifecycleState>(allowIntegerValues: false));
}

public partial class Program;
