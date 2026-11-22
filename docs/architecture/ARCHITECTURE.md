# Architecture

FactoryPulse AI starts as a modular monolith with one deployable ASP.NET Core process. Dependencies point toward the domain and use cases. Infrastructure adapters are added as simulation or storage needs arise.

| Project | Responsibility | Direct project references |
| --- | --- | --- |
| `FactoryPulse.Domain` | Domain rules and models | None |
| `FactoryPulse.Application` | Use cases and boundary contracts | Domain |
| `FactoryPulse.Infrastructure` | External adapters | Application |
| `FactoryPulse.Api` | HTTP endpoints, middleware, dependency registration | Application, Infrastructure |
| `FactoryPulse.UnitTests` | Application behavior in isolation | Application |
| `FactoryPulse.IntegrationTests` | API behavior through an in-process host | API |

Infrastructure may reference Domain directly when an adapter requires it. Domain and Infrastructure are class library projects awaiting their first domain rule and adapter, respectively.

## Request flow

`GET /api/status` enters `StatusController`, which delegates to `ApplicationStatusService`. The service returns the configured instance name, the fixed simulation mode, and UTC time from `TimeProvider`. API startup binds and validates `ApplicationStatus:InstanceName` and composes the use case. In deployments, the name can be overridden with `ApplicationStatus__InstanceName` and `AllowedHosts` can be set to the allowed hostnames.

`GET /health` uses ASP.NET Core health checks to report process liveness. Dependency readiness checks belong in a later stage when external dependencies exist. The host provides structured logging and a centralized exception handler with ProblemDetails. Development exposes OpenAPI and uses a loopback HTTPS launch profile. The integration suite runs under `Testing` with TestServer and requires no development certificate. Production inherits base settings and can receive protected settings at deployment time.

## Verification

Unit tests control the application clock to assert status deterministically. Integration tests exercise the real pipeline and receive HTTP responses from the in-process server. Both suites use xUnit v3 on Microsoft.Testing.Platform with Coverlet for coverage. The [README](../../README.md) lists the restore, build, test, and format commands.
