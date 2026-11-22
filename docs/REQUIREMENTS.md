# Requirements

FactoryPulse AI demonstrates industrial software engineering through a lightweight, bilingual simulation of machine monitoring, quality inspection, and predictive maintenance. The application must never control real equipment.

## Day 1, Stage 1 acceptance criteria

- Provide a Visual Studio solution with four .NET 10 production projects and two xUnit projects. Follow the dependency rules in the [architecture](architecture/ARCHITECTURE.md).
- Use centralized stable NuGet versions, a stable SDK selection policy, nullable references, analyzers, and consistent formatting.
- Expose process liveness at `GET /health` and application status at `GET /api/status`. Use controller routing for the application endpoint.
- Supply development HTTPS and OpenAPI, built-in structured logging, centralized ProblemDetails handling, typed configuration with startup validation, and local User Secrets support.
- Include a meaningful application test, an API liveness test through WebApplicationFactory, and reproducible restore, build, test, and format checks.
- Add English and German project overviews, persistent development instructions, architecture documentation, and the initial decision record.

## Release roadmap

| Release | Planned scope |
| --- | --- |
| 1.0, first three days | ASP.NET Core, Angular, an industrial machine simulator, EF Core and SQLite, REST and SignalR, live monitoring, alarms and history, English and German switching, automated tests, Docker Compose, and GitHub Actions CI/CD. |
| 2.0, following four days | WPF with MVVM and MEF plugins, machine and camera adapters, CSV replay, OpenCV, an independent vision service when justified, ML.NET predictive maintenance, Kubernetes demonstration, benchmarks, and expanded tests. OPC UA and ONNX are feasibility-dependent. |

Future features should retain the simulation boundary, use bounded resources where applicable, and substantiate performance claims with repeatable measurements.
