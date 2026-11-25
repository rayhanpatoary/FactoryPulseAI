# FactoryPulse AI development instructions

## Product and release strategy

FactoryPulse AI is a modular industrial platform designed for machine monitoring, operational diagnostics, intelligent quality inspection, and manufacturing analytics. Current development operates within a simulation boundary. The platform must never control real equipment.

- The Version 1.0 scope covers the ASP.NET Core API, Angular web client, C# simulator, EF Core with SQLite, REST and SignalR, monitoring, alarms, history, localization, tests, Docker Compose, and GitHub Actions.
- The Version 2.0 scope may add WPF, CommunityToolkit.Mvvm, System.Composition, plugins, CSV replay, camera and OpenCV integrations, predictive maintenance, benchmarking, and a Kubernetes demonstration. OPC UA, ONNX inference, and service extraction depend on feasibility and an explicit scope decision.
- Deliver only the current authorized stage. Later release items are roadmap work.

## Product Documentation and Professional Communication Standards

- Present FactoryPulse AI as an industrial software product. Describe the problems it addresses, intended users, architecture, and verified capabilities. Keep portfolio, student, tutorial, and job-search framing out of public documentation.
- Keep internal schedules, prompts, and implementation notes outside public-facing product descriptions. Distinguish the product vision, implemented functionality, and the roadmap. Use evidence from the repository to assign implementation status.
- Use precise technical language. Avoid repetitive promotion, buzzwords, decorative emojis, and claims such as “enterprise-grade” without evidence. Verify claims about functionality, metrics, certifications, deployments, readiness, and code origin or authorship before publishing them.
- Check capability claims against source code and automated tests. Recheck the public documentation when functionality changes and keep build, test, and run instructions synchronized with the repository.
- Maintain factually equivalent, idiomatic English and German READMEs with consistent technical terminology, naming, and formatting. Keep the READMEs concise and oriented toward readers who are new to the product.
- Record detailed design and trade-offs under `docs/architecture` and architecture decisions under `docs/adr`. Explain the rationale when it helps maintainers and favor simple, maintainable solutions.
- Review documentation diffs, Markdown formatting, relative links, and language parity before committing. Commit by documentation topic and follow the permanent atomic commit policy.

## Mandatory engineering rules

- Start with a modular monolith. Apply SOLID, KISS, DRY and YAGNI through separation of concerns, dependency inversion and composition. Add ports for actual boundaries and introduce patterns only when they solve a concrete problem. Use EF Core directly inside its adapter when persistence is introduced.
- Domain has no dependencies on other application projects. Application may depend on Domain. Infrastructure may depend on Application and Domain as needed. API is the composition root and references Application and Infrastructure. Test projects reference only their subjects. Keep the dependency graph acyclic.
- Use stable .NET 10 LTS dependencies managed in `Directory.Packages.props`. Keep nullable references and analyzers enabled. Follow `.editorconfig`, Microsoft naming conventions and modern C# practices. Prefer small classes and constructor injection. Comment significant decisions. Use asynchronous I/O, propagate cancellation, and dispose owned resources.
- Bind application settings with typed Options and validate required settings on startup. Put safe defaults in `appsettings.json` and use environment-specific settings where useful. Keep credentials in User Secrets for local development and protected environment variables or external providers for deployment. Keep secrets out of source, examples, telemetry, and client assets.
- Write source code, identifiers, comments, and technical documentation in English. User-facing interfaces must support English and German once introduced. Keep translatable text and culture-sensitive formatting at presentation boundaries. Maintain both project overviews.
- Keep resource use predictable. Bound queues when introduced, use configurable sampling, and add pagination and indexes with data access. Use structured logs and health checks. Measure before adding caching or concurrency and support published performance claims with reproducible results.
- Use xUnit for behavior tests and WebApplicationFactory for API integration. Keep tests deterministic and independent. Use Coverlet for .NET coverage. Add Moq or Testcontainers when a concrete test benefits. Use Vitest and Playwright for Angular and evaluate FlaUI with WPF. Run analyzers and `dotnet format` for .NET changes. Add ESLint and Prettier with Angular and Ruff if Python is introduced.

## Git Workflow and Atomic Commit Policy

- Commit each verified, logically complete change throughout every stage and technology. Aim for roughly 80–100 changed handwritten lines per commit and commit smaller finished changes promptly. Keep necessary multi-file changes and generated migrations together.
- Use descriptive English Conventional Commit messages. Stage explicit paths or selected hunks. Preserve unrelated user changes and staged work.
- Build and run relevant automated tests before each commit. Investigate failures and report unresolved blockers. Keep broken changes out of commits.
- Inspect both `git diff --cached` and `git diff --cached --stat` before committing. Exclude secrets, databases and sidecar files, machine-local settings, build outputs, and temporary files.
- Preserve history. Avoid squashing, amending, rewriting history, force-pushing, unnecessary merge commits, and automatic pushes.
- Continue through the authorized stage after each commit. Report the short hash and subject of each new commit at handoff.

## Git Branching, Integration and Merge Policy

- Start each independently meaningful feature or bug fix from the latest suitable local `main` before editing. Use `feature/<name>` or `bugfix/<name>` with a short, meaningful suffix containing lowercase letters, digits and hyphens. Limit the whole branch name to 15 characters. Keep implementation commits off `main` and keep each branch focused on its own change and related tests, configuration and documentation.
- Follow the atomic commit policy above. Keep tests close to their changes, build and test before committing, use Conventional Commits, and stage and inspect only related files. Preserve user work and history. Exclude secrets, local databases and generated temporary files.
- At completion, commit the feature, verify the working tree is clean and confirm the intended branch is active. Merge the latest local `main` **into** that branch. Resolve conflicts while preserving feature changes and unrelated work. An already up-to-date result is valid.
- On the working branch after integration, rerun restore, build, relevant tests, formatting and analyzers. Confirm the branch and final Git status. Leave the branch active for review. Automatic merges into `main`, pushes and remote pull requests require separate authorization.
- Report the branch, change summary, commit hashes and subjects, build and test results, main-to-branch integration outcome, post-merge verification, final Git status and review readiness.

## Workflow and definition of done

1. Read the current requirements, architecture, and relevant ADRs. Inspect the existing implementation and preserve unrelated changes.
2. Implement a small end-to-end change for the current stage. Update documentation and ADRs when a public contract, operational behavior, or architecture decision changes.
3. Restore, build, run applicable tests, and verify formatting. Exercise the affected endpoints or entry points and record any blocked checks.
4. Finish with a compiling, tested change, current run instructions, and a clear account of prerequisites and unresolved issues. Start another stage only when requested.
