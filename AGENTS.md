# FactoryPulse AI development instructions

## Product and release strategy

FactoryPulse AI is a portfolio project for the German software engineering market. It demonstrates .NET, industrial software, and full-stack development through simulated machine monitoring, quality inspection, and predictive maintenance. The platform must operate in simulation and must never control real equipment.

- Version 1.0 is planned for the first three development days. It covers the ASP.NET Core API, Angular web client, C# simulator, EF Core with SQLite, REST and SignalR, monitoring, alarms, history, localization, tests, Docker Compose, and GitHub Actions.
- Version 2.0 is planned for the following four development days. It may add WPF, CommunityToolkit.Mvvm, System.Composition, plugins, CSV replay, camera and OpenCV integrations, predictive maintenance, benchmarking, and a Kubernetes demonstration. OPC UA, ONNX inference, and service extraction depend on feasibility and an explicit scope decision.
- Deliver only the current authorized stage. Later release items are roadmap work.

## Mandatory engineering rules

- Start with a modular monolith. Apply SOLID, KISS, DRY and YAGNI through separation of concerns, dependency inversion and composition. Add ports for actual boundaries and introduce patterns only when they solve a concrete problem. Use EF Core directly inside its adapter when persistence is introduced.
- Domain has no dependencies on other application projects. Application may depend on Domain. Infrastructure may depend on Application and Domain as needed. API is the composition root and references Application and Infrastructure. Test projects reference only their subjects. Keep the dependency graph acyclic.
- Use stable .NET 10 LTS dependencies managed in `Directory.Packages.props`. Keep nullable references and analyzers enabled. Follow `.editorconfig`, Microsoft naming conventions and modern C# practices. Prefer small classes and constructor injection. Comment significant decisions. Use asynchronous I/O, propagate cancellation, and dispose owned resources.
- Bind application settings with typed Options and validate required settings on startup. Put safe defaults in `appsettings.json` and use environment-specific settings where useful. Keep credentials in User Secrets for local development and protected environment variables or external providers for deployment. Keep secrets out of source, examples, telemetry, and client assets.
- Write source code, identifiers, comments, and technical documentation in English. User-facing interfaces must support English and German once introduced. Keep translatable text and culture-sensitive formatting at presentation boundaries. Maintain both project overviews.
- Keep resource use predictable. Bound queues when introduced, use configurable sampling, and add pagination and indexes with data access. Use structured logs and health checks. Measure before adding caching or concurrency and support published performance claims with reproducible results.
- Use xUnit for behavior tests and WebApplicationFactory for API integration. Keep tests deterministic and independent. Use Coverlet for .NET coverage. Add Moq or Testcontainers when a concrete test benefits. Use Vitest and Playwright for Angular and evaluate FlaUI with WPF. Run analyzers and `dotnet format` for .NET changes. Add ESLint and Prettier with Angular and Ruff if Python is introduced.

## Workflow and definition of done

1. Read the current requirements, architecture, and relevant ADRs. Inspect the existing implementation and preserve unrelated changes.
2. Implement a small end-to-end change for the current stage. Update documentation and ADRs when a public contract, operational behavior, or architecture decision changes.
3. Restore, build, run applicable tests, and verify formatting. Exercise the affected endpoints or entry points and record any blocked checks.
4. Finish with a compiling, tested change, current run instructions, and a clear account of prerequisites and unresolved issues. Start another stage only when requested.
