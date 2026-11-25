# Architecture

FactoryPulse AI starts as a modular monolith with one deployable ASP.NET Core process. Dependencies point toward the domain and use cases. Infrastructure adapters are added as simulation or storage needs arise.

| Project | Responsibility | Direct project references |
| --- | --- | --- |
| `FactoryPulse.Domain` | Machine, reading and alarm models and validation | None |
| `FactoryPulse.Application` | Use cases and boundary contracts | Domain |
| `FactoryPulse.Infrastructure` | SQLite mappings, migrations and history queries | Application, Domain |
| `FactoryPulse.Api` | HTTP endpoints, middleware, dependency registration | Application, Infrastructure |
| `FactoryPulse.UnitTests` | Application and domain behavior in isolation | Application, Domain |
| `FactoryPulse.IntegrationTests` | API behavior and relational persistence | API, Infrastructure |

Infrastructure maps the domain entities to SQLite through `FactoryPulseDbContext`. The API registers the scoped context and history queries using validated database path options. EF Core tooling uses the API as its startup project so it resolves the same configuration and provider. Schema migrations run explicitly as a setup or deployment step. [Database design and migration commands](DATABASE.md).

## Request flow

`GET /api/status` enters `StatusController`, which delegates to `ApplicationStatusService`. The service returns the configured instance name, the fixed simulation mode, and UTC time from `TimeProvider`. API startup binds and validates `ApplicationStatus:InstanceName` and composes the use case. In deployments, the name can be overridden with `ApplicationStatus__InstanceName` and `AllowedHosts` can be set to the allowed hostnames.

`GET /health` uses ASP.NET Core health checks to report process liveness. Dependency readiness checks belong in a later stage when external dependencies exist. The host provides structured logging and a centralized exception handler with ProblemDetails. Development exposes OpenAPI and uses a loopback HTTPS launch profile. The integration suite runs under `Testing` with TestServer and requires no development certificate. Production inherits base settings and can receive protected settings at deployment time.

## Verification

Unit tests verify application and domain rules with fixed timestamps. API integration tests exercise the real pipeline through an in-process server. Persistence tests apply the migrations in isolated SQLite databases and verify the relationships, constraints, acknowledgement updates, and paged queries. Both suites use xUnit v3 on Microsoft.Testing.Platform with Coverlet for coverage. The [README](../../README.md) lists the restore, build, test, and format commands.
