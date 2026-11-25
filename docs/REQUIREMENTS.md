# Product and engineering requirements

FactoryPulse AI is designed to bring machine readings, operating states, and alarms together for industrial condition monitoring and fault diagnosis. Industrial software engineers, maintenance engineers, equipment operators, and quality teams are the intended users. Current development operates within a simulation boundary. Future acquisition adapters may read equipment data. The platform must never control real equipment.

## Platform requirements

- Use a modular .NET 10 solution with independent domain rules, application use cases, external adapters, and an ASP.NET Core composition root. [Architecture and project dependencies](architecture/ARCHITECTURE.md)
- Expose API process liveness at `GET /health` and application status at `GET /api/status`. Provide local development HTTPS, OpenAPI in Development, structured logging, centralized ProblemDetails handling, and typed configuration validation.
- Store machines, readings, and alarms with validated domain values, UTC timestamps, referential integrity, and bounded history queries. Apply database migrations explicitly and keep local database files out of version control. [Database design](architecture/DATABASE.md)
- Pin a stable SDK and manage stable NuGet versions centrally. Enable nullable reference types, analyzers, and source formatting. Provide reproducible build and test commands and verify the HTTP pipeline and relational behavior with integration tests.
- Keep credentials outside source control. Provide English and German public documentation and plan localization for future operator interfaces.

## Requirements for future capabilities

- Bound resource use in acquisition and monitoring workflows. Validate any published performance figures with reproducible measurements.
- Keep data source integrations behind adapter boundaries and retain read-only behavior for equipment connections.
- Evaluate standalone analytics services when independent deployment or scaling solves a demonstrated need.

The [product overview](../README.md) records implementation status and roadmap priorities.
