# ADR 0001 — Start with a modular monolith

**Status:** Accepted

**Date:** 2026-11-22

## Context

FactoryPulse AI must demonstrate industrial application architecture while remaining easy to run, verify, and extend. The initial stage needs one HTTP host and an application use case. Storage and simulator adapters will gain concrete contracts as the monitoring slice is developed.

## Decision

Use a modular monolith with Domain, Application, Infrastructure, and API projects on .NET 10 LTS. Keep domain logic independent, define use cases in Application, implement external adapters in Infrastructure, and register them in the API. Use ASP.NET Core and the built-in DI container. Manage package versions centrally and test application behavior separately from the HTTP host.

## Consequences

One deployable process keeps local setup and operational overhead low. Project boundaries make dependency direction visible and permit adapters to evolve independently. Shared in-process deployment means a single release and scaling unit. Revisit service boundaries when a workload such as vision processing has demonstrated independent deployment or scaling needs.
