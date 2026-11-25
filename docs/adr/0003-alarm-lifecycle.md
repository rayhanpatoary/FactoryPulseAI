# ADR 0003 — Persist telemetry-driven alarm episodes

**Status:** Accepted

## Context

An abnormal condition can span many telemetry samples. Operators need to see when it began and ended and whether someone acknowledged it.

## Decision

Evaluate thresholds on every sampled frame using Application rules. Process frames in sequence within the ingestion session and persist a telemetry batch immediately whenever an alarm opens or clears. Use one SQLite transaction for the batch and its alarm transitions. Store creation, acknowledgement, and resolution times on the existing alarm entity. Enforce at most one active row per machine and alarm code with a filtered unique index.

Expose bounded alarm queries via HTTP. Restrict acknowledgement to Development and Testing until operator authorization is implemented. Preserve the first acknowledgement with a conditional database update.

## Consequences

The process can resume alarm evaluation from persisted active rows after restarting. Ordinary readings keep the configured batch cadence, while transitions may cause additional writes. Hosts should use one ordered ingestion session for each machine. [Settings, API, and lifecycle](../architecture/ALARMS.md)
