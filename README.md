# FactoryPulse AI

[Deutsch](README.de.md)

**Industrial Monitoring, Diagnostics & Intelligent Quality Inspection Platform**

FactoryPulse AI is designed to bring machine readings, equipment status, and alarms together for condition monitoring and fault diagnosis. It is intended for industrial software engineers, maintenance teams, equipment operators, and manufacturing quality teams who need a shared view of operating history and faults. The product direction includes camera-based inspection and manufacturing analytics.

## Current capabilities

- **Status API.** ASP.NET Core exposes process liveness and application status, with an OpenAPI document in Development.
- **Machine data model.** Domain rules validate machines, timestamped readings, and alarms. Alarm acknowledgement preserves the first acknowledgement time.
- **Simulated acquisition.** An opt-in simulator generates repeatable telemetry for three CNC profiles and writes the latest readings to SQLite in batches.
- **Machine monitoring API.** Read the machine catalog, status counts, latest readings, and paginated history with UTC date filters. [Endpoints and examples](docs/architecture/MONITORING-API.md)
- **SQLite persistence.** EF Core stores machine records, readings, and alarms with indexed relationships and explicit schema migrations.

## Architecture and technology

The solution is a modular monolith on .NET 10. Its four production projects keep domain rules independent of storage and HTTP delivery.

| Project | Responsibility |
| --- | --- |
| `FactoryPulse.Domain` | Machine, reading, and alarm models and validation |
| `FactoryPulse.Application` | Use cases and boundary contracts, including application status |
| `FactoryPulse.Infrastructure` | EF Core 10 and SQLite mappings, migrations, and history queries |
| `FactoryPulse.Api` | ASP.NET Core endpoints, configuration, dependency injection, and OpenAPI |

The simulator implements the application's machine data-source contract. The module boundaries support future data-source adapters, web and desktop clients, and the later extraction of analytics services. [System architecture](docs/architecture/ARCHITECTURE.md)

## Getting started

Use the .NET 10 SDK specified in [global.json](global.json), currently 10.0.404 with patch roll-forward within its feature band. Run the commands from the repository root in PowerShell. If the SDK is installed under `.dotnet/` on Windows, use `.\.dotnet\dotnet.exe` in place of `dotnet`.

```powershell
dotnet restore FactoryPulseAI.slnx
dotnet build FactoryPulseAI.slnx --no-restore
```

To initialize SQLite, restore the repository-local EF tool and apply the migration explicitly. The database directory must be writable. [Database and migration guide](docs/architecture/DATABASE.md)

```powershell
dotnet tool restore
dotnet ef database update --project src/FactoryPulse.Infrastructure --startup-project src/FactoryPulse.Api --context FactoryPulseDbContext
```

Trust the ASP.NET Core development certificate if needed, then start the HTTPS profile. For the IDE workflow, use Visual Studio 2026 with the ASP.NET and web development workload. Open [FactoryPulseAI.slnx](FactoryPulseAI.slnx), set `FactoryPulse.Api` as the startup project, select **FactoryPulse.Api (HTTPS)**, and press **F5**.

```powershell
dotnet dev-certs https --trust
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)" --no-build
```

| Local endpoint | Purpose |
| --- | --- |
| `https://localhost:7161/health` | API process liveness |
| `https://localhost:7161/api/status` | Instance name, simulation mode, and UTC observation time |
| `https://localhost:7161/openapi/v1.json` | OpenAPI document in Development |

## Configuration and security

ASP.NET Core loads defaults from `appsettings.json` and validates typed settings at startup. Override `Database__Path` to select a writable SQLite file, `ApplicationStatus__InstanceName` to name the instance, and `AllowedHosts` to configure accepted host names. Use .NET User Secrets for development credentials and protected environment variables or a secret provider for deployments. Keep credentials out of version control.

After applying the database migration, set `Simulation__Enabled=true` before starting the API to record simulated readings. [Simulator setup and settings](docs/architecture/SIMULATION.md)

## Testing and quality assurance

xUnit tests exercise domain rules, the HTTP pipeline through WebApplicationFactory, and migrations and queries against isolated SQLite databases. Microsoft.Testing.Platform runs the suite, and Coverlet collects coverage. Central package version management, nullable reference types, .NET analyzers, and `dotnet format` support consistent builds. [Engineering requirements](docs/REQUIREMENTS.md)

```powershell
dotnet test --solution FactoryPulseAI.slnx --no-build --coverlet
dotnet format FactoryPulseAI.slnx --verify-no-changes --no-restore
```

## Implementation status

| Capability | Status |
| --- | --- |
| Application status, liveness, and Development OpenAPI | Implemented |
| Machine, reading, and alarm models and SQLite persistence | Implemented |
| Simulated machine telemetry and batch storage | Implemented |
| Read-only machine catalog, status summary, and telemetry API | Implemented |
| End-to-end machine monitoring | In progress. Simulated acquisition and REST queries exist. Streaming and operator interfaces are planned. |
| Quality inspection, predictive analytics, and deployment automation | Planned |

## Roadmap

- **Telemetry and diagnostics.** Deliver live telemetry with SignalR. Evaluate OPC UA for read-only data acquisition and support additional sources through plugins.
- **Operator interfaces.** Build an Angular web interface and a WPF desktop client for machine status, trends, and alarms.
- **Inspection and analytics.** Add camera-based quality inspection with OpenCV and evaluate predictive maintenance with ML.NET.
- **Deployment and operations.** Add Docker and Docker Compose, automate verification with GitHub Actions, and evaluate Kubernetes for orchestration.

[Initial architecture decision](docs/adr/0001-initial-architecture.md)
