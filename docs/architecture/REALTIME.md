# Real-time monitoring

Connect a SignalR client to `/hubs/monitoring`. The hub broadcasts machine readings and alarm transitions. The REST API provides the initial snapshot, current alarm state, and persisted history. [Machine REST API](MONITORING-API.md) · [Alarm REST API](ALARMS.md)

## JSON events

Register handlers for these event names. Each invocation carries one object with camel-case properties. Times are UTC and serialize with a `Z` suffix. Severity and machine status use the same string values as REST. Use alarm codes as keys for localized display.

| Event | Fields |
| --- | --- |
| `MachineReadingUpdated` | Machine ID, machine code and status when available, sample time, temperature (°C), vibration (mm/s), RPM, torque (N·m), and tool wear (0–1) |
| `AlarmOpened` | Alarm ID, machine ID, alarm code, severity, creation time, active state, and acknowledgement state |
| `AlarmResolved` | Alarm ID, machine ID, alarm code, and resolution time |
| `AlarmAcknowledged` | Alarm ID, machine ID, acknowledgement time, and acknowledgement state |

These examples show the payload of each invocation, with the event name as the object key for reference.

```json
{
  "MachineReadingUpdated": {
    "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
    "machineCode": "CNC-001",
    "timestampUtc": "2026-01-05T08:00:00Z",
    "temperature": 82.1,
    "vibration": 1.3,
    "rpm": 2400,
    "torque": 18,
    "toolWear": 0.1,
    "status": "Running"
  },
  "AlarmOpened": {
    "alarmId": 12,
    "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
    "alarmCode": "TEMPERATURE_WARNING",
    "severity": "Warning",
    "createdAtUtc": "2026-01-05T08:00:00Z",
    "isActive": true,
    "isAcknowledged": false
  },
  "AlarmResolved": {
    "alarmId": 12,
    "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
    "alarmCode": "TEMPERATURE_WARNING",
    "resolvedAtUtc": "2026-01-05T08:02:00Z"
  },
  "AlarmAcknowledged": {
    "alarmId": 12,
    "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
    "acknowledgedAtUtc": "2026-01-05T08:01:00Z",
    "isAcknowledged": true
  }
}
```

## Publishing and delivery

The simulator sends live readings at the configured sample cadence using the reading and machine metadata it already has. A live reading may precede its scheduled SQLite write, and ordinary samples between writes may never appear in history. The processor publishes alarm openings and resolutions after the telemetry and alarm transaction succeeds. The first successful REST acknowledgement publishes after the database update. Repeating the acknowledgement returns the same stored timestamp without another event.

The API implements the Application publisher with SignalR's `IHubContext`. Each send batch uses a 250 ms cancellation budget. A sampled frame may send an alarm batch followed by a reading batch. A failed or expired send can leave some events undelivered. The publisher skips retries and rate-limits its warning to once per minute. Request cancellation or shutdown can also interrupt delivery after a database commit. The hub provides best-effort delivery to connected clients and keeps no replay history. This configuration assumes one API instance. Multi-instance operation needs a SignalR scale-out mechanism.

## Client synchronization

1. Register handlers before connecting. Buffer incoming updates while fetching the snapshot from `GET /api/machines`, the latest-reading endpoints, and the paginated active alarm list. Keep the buffer bounded and retry the snapshot if it overflows.
2. Apply readings by machine ID and sample timestamp. Apply lifecycle events by alarm ID, merging acknowledgement and resolution independently. A late open event must preserve acknowledgement or resolution already known for that ID.
3. Events can arrive during or after the snapshot, including between pages. Deduplicate overlapping updates and refresh REST state if the order is ambiguous. A snapshot gives the client persisted state, while live events can carry newer samples.
4. On disconnection, mark displayed telemetry stale. Reconnect with backoff, refresh the REST snapshot, and reconcile events received while loading it. REST supplies the recovery path for missed alarm events and persisted readings.

## Configuration and local verification

`RealtimeMonitoring:Enabled` defaults to `true` and enables the authenticated hub in Development and Testing. Production also requires `RealtimeMonitoring:AllowProduction=true`. The read policy applies to every mapped hub. Browser requests with an Origin header must match the request origin, including for WebSockets. The hub has no client-callable operations. [Authentication, opt-in and session expiry](AUTHENTICATION.md)

Apply the migrations, provision an account, and start the HTTPS profile with the simulator enabled to stream readings. Sign in to the dashboard and check its live connection status. [Local provisioning](AUTHENTICATION.md) · [Simulator settings](SIMULATION.md)

Run `dotnet test --solution FactoryPulseAI.slnx --no-build` after building to exercise SignalR connections, payloads, alarm commits, disconnect cleanup, and the environment restriction with an in-process server and controlled simulator time.
