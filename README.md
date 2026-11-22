# FactoryPulse AI

[Deutsche Projektübersicht](README.de.md)

FactoryPulse AI is a portfolio project for simulated industrial machine monitoring, quality inspection, and predictive maintenance. Day 1, Stage 1 delivers a .NET 10 solution with a small ASP.NET Core API and automated tests. The project uses simulated data and operates without a connection to industrial equipment.

## Run locally

Use Visual Studio Community 2026 with the ASP.NET and web development workload and a stable .NET 10.0.4xx SDK at version 10.0.404 or newer within that feature band. [`global.json`](global.json) also supports a workspace-local SDK in `.dotnet/`.

1. Open `FactoryPulseAI.slnx` in Visual Studio.
2. Set `FactoryPulse.Api` as the startup project and select **FactoryPulse.Api (HTTPS)**.
3. Press **F5**. If prompted, review and accept the local ASP.NET Core development certificate to use HTTPS.
4. Open **Test > Run All Tests** to run the unit and integration tests.

The launch profile exposes these local HTTPS endpoints.

| Endpoint | Purpose |
| --- | --- |
| `https://localhost:7161/health` | ASP.NET Core process liveness |
| `https://localhost:7161/api/status` | Configured instance name, simulation mode, and UTC observation time |
| `https://localhost:7161/openapi/v1.json` | OpenAPI document in Development |

The CLI supports the same verification workflow.

```powershell
dotnet restore FactoryPulseAI.slnx
dotnet build FactoryPulseAI.slnx --no-restore
dotnet test --solution FactoryPulseAI.slnx --no-build --coverlet
dotnet format FactoryPulseAI.slnx --verify-no-changes --no-restore
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)"
```

On Windows, use `.\.dotnet\dotnet.exe` in place of `dotnet` if the SDK was unpacked into the workspace and `dotnet` is unavailable on `PATH`. To set up a missing development certificate from the CLI, run `dotnet dev-certs https --trust` and confirm the trust prompt.

## Project details

- [Requirements and release scope](docs/REQUIREMENTS.md)
- [Architecture and verification](docs/architecture/ARCHITECTURE.md)
- [Initial architecture decision](docs/adr/0001-initial-architecture.md)
- [Development instructions](AGENTS.md)
