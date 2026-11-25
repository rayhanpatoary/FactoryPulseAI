using FactoryPulse.Api.Persistence;
using FactoryPulse.Application.Status;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.AddFactoryPulseDatabase();

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

app.UseExceptionHandler();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
