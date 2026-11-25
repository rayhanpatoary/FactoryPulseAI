# ADR 0002 — Project monitoring reads at the persistence boundary

**Status:** Accepted

## Context

Clients need a stable HTTP contract for machine metadata and simulator telemetry. The existing SQLite schema indexes readings by machine and UTC timestamp. The amount of stored history can grow independently of the number of readings each client can display.

## Decision

Keep monitoring read contracts in Application, validate HTTP inputs in the API, and project DTOs within Infrastructure queries. Use offset pagination with bounded page numbers and sizes. Fetch one additional row to report whether more readings match. Order by UTC timestamp and reading ID. Use half-open UTC time ranges so adjacent windows can share an endpoint without overlapping.

## Consequences

The API gets predictable response sizes and uses the existing SQLite index. Offset pagination can shift as readings arrive and deep offsets still require database work. UTC bounds let clients narrow the query window. Review cursor pagination if measured read workloads require it. [HTTP contract](../architecture/MONITORING-API.md)
