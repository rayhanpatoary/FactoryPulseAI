# ADR 0005 — Use REST snapshots and SignalR updates in the web client

**Status:** Accepted

## Context

Monitoring views need persisted records, live readings, and runtime English and German display. Some environments cannot expose the monitoring hub.

## Decision

Use a standalone Angular client with relative REST and SignalR paths. Connect and subscribe before loading the initial snapshot. Merge live readings using their UTC timestamps and refresh alarm state through REST when lifecycle notifications arrive. Load telemetry charts with the machine detail route and cap recent live samples.

Use a loopback development proxy with verified HTTPS to the API. Default acknowledgement controls to disabled and continue to enforce the existing server policy.

## Consequences

The same client can show persisted data when SignalR is unavailable. A reconnect requires a fresh REST snapshot. Live readings and persisted history update on different schedules. [Web dashboard](../architecture/WEB.md)
