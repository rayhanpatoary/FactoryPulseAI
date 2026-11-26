# ADR 0004 — Stream samples and committed alarm transitions with SignalR

**Status:** Accepted

## Context

Monitoring clients need live samples and alarm notifications. The simulator samples more frequently than it writes ordinary readings. SQLite already provides the authoritative alarm history.

## Decision

Use the existing simulator and alarm persistence flow. Map the sampled readings to small Application events without additional database reads. Publish lifecycle updates after commit and send the first acknowledgement only. Put the publishing port in Application and its SignalR implementation in the API.

Use a bounded send deadline for each batch. Keep the hub read-only and map it only in Development and Testing until an authenticated deployment boundary is available.

## Consequences

Live samples can arrive before persistence and may be absent from history. SignalR deliveries can be interrupted by disconnects, cancellation, or slow clients. Clients refresh authoritative REST state when connecting or reconnecting. Each API instance can deliver events to its own connected clients. [SignalR contracts and client synchronization](../architecture/REALTIME.md)
