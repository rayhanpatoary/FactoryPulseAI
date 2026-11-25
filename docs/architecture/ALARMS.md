# Machine alarm management

FactoryPulse AI evaluates simulated temperature and vibration readings. The limits below are demonstration settings for condition monitoring. The platform operates in simulation.

## Thresholds and lifecycle

| Alarm code | Severity | Trigger setting and default | Clear setting and default |
| --- | --- | --- | --- |
| `TEMPERATURE_WARNING` | Warning | `TemperatureWarning`, 80 °C | `TemperatureWarningClear`, 77 °C |
| `TEMPERATURE_CRITICAL` | Critical | `TemperatureCritical`, 95 °C | `TemperatureCriticalClear`, 90 °C |
| `VIBRATION_WARNING` | Warning | `VibrationWarning`, 5 mm/s | `VibrationWarningClear`, 4 mm/s |

An alarm opens when the measurement reaches or exceeds its trigger. It clears when the measurement falls strictly below its clear limit. The gap provides hysteresis. Temperature has one active severity at a time. Escalation resolves the warning and opens a critical alarm. Clearing a critical alarm opens a warning if the temperature is still at or above the warning clear limit. Vibration is evaluated independently.

An alarm starts as `ActiveUnacknowledged`. Acknowledgement moves an active alarm to `ActiveAcknowledged` and records the first acknowledgement time. When the condition clears, the alarm becomes `Resolved` and keeps its acknowledgement, if present. A subsequent occurrence gets a new ID. A resolved alarm can also be acknowledged for the historical record.

Configure the validated `AlarmDetection` section in `appsettings.json` or use environment overrides and restart the API. Temperature settings must be finite and ordered as warning clear < warning trigger ≤ critical clear < critical trigger. Vibration limits must satisfy 0 ≤ clear < trigger and the trigger must be finite. `Enabled` defaults to `true`. Disabling detection pauses automatic transitions while telemetry storage continues.

The default CNC profiles are configured below the default triggers. To observe a temperature warning with the simulator, apply the database migrations and run the following from the repository root. [Database and migration guide](DATABASE.md)

```powershell
$env:Simulation__Enabled = 'true'
$env:AlarmDetection__TemperatureWarning = '60'
$env:AlarmDetection__TemperatureWarningClear = '57'
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)"
```

## Storage and processing

The shared processor accepts machine readings independently of how they were generated. It loads active alarms for the monitored machines at session startup and retains the latest reading and active condition state per machine. Each session processes frames sequentially and ignores readings older than the latest accepted observation for that machine in the session. The current host uses one ingestion loop for its machines.

Without a transition, telemetry is persisted on the configured batch schedule. A transition writes the current telemetry batch and the affected alarm rows together immediately. SQLite enforces uniqueness for active machine and alarm code pairs. The migration preserves legacy history and reconciles duplicate episodes before creating the unique index. [Database design](DATABASE.md)

## REST API

| Method and path | Response |
| --- | --- |
| `GET /api/alarms` | A page of active and resolved alarms. Supports `machineId`, `severity`, `code`, and `active`. |
| `GET /api/alarms/active` | A page of active alarms, including acknowledged alarms. Supports `machineId`, `severity`, and `code`. |
| `GET /api/machines/{id}/alarms` | A page for one machine. Supports `severity`, `code`, and `active`. An unknown machine returns `404`. |
| `POST /api/alarms/{id}/acknowledge` | The updated alarm with `200`, or `404` for an unknown alarm. Takes no body. |

Lists are ordered newest first by creation time and ID. The response contains `page`, `pageSize`, `hasMore`, and `items`. Pass `page` from 1 to 10,000 and `pageSize` from 1 to 100. The defaults are 1 and 25. An item includes `id`, `machineId`, `alarmCode`, `message`, `severity`, `state`, `createdAtUtc`, `acknowledgedAtUtc`, and `resolvedAtUtc`. The last two are nullable. Alarm codes are stable identifiers suitable for localization.

```powershell
Invoke-RestMethod 'https://localhost:7161/api/alarms?active=true&severity=Warning&pageSize=10'
Invoke-RestMethod -Method Post 'https://localhost:7161/api/alarms/1/acknowledge'
```

Use an ID returned by the list when acknowledging. Repeating the request returns the original acknowledgement time and the current alarm state. The write is available only in Development and Testing. Other environments return `403` for valid IDs until operator authorization is available. Invalid inputs return `400` with ProblemDetails.

## Verification

xUnit covers exact trigger boundaries, hysteresis, temperature severity transitions, independent vibration, lifecycle rules, and invalid settings. SQLite integration tests verify migration upgrade and rollback, transactional persistence, deduplication, restart behavior, and immediate writes through the simulator host. WebApplicationFactory tests cover filtering and pagination, machine scoping, acknowledgement and idempotency, validation responses, and the environment restriction.
