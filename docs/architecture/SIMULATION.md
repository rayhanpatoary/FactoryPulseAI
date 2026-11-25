# Machine simulation

The simulator is an opt-in source of repeatable development telemetry. It runs in the API process and uses the existing SQLite machine and reading tables.

## Run locally

Build the solution and apply the SQLite migration before enabling the simulator. [Database setup](DATABASE.md)

```powershell
$env:Simulation__Enabled = 'true'
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)"
```

Set `Simulation__Enabled` to `false` before startup to disable the worker. The default is disabled, including in the test host. Settings are validated on startup and changes take effect when the process restarts.

## Configuration

The API's `Simulation` section supports JSON settings and ASP.NET Core environment overrides, such as `Simulation__Profiles__0__TargetRpm`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Enables simulated acquisition and persistence |
| `Seed` | `42` | Integer seed combined with each profile's ID for independent random streams |
| `SamplingInterval` | 1 second | Time between samples, from 100 ms to 1 minute |
| `PersistenceInterval` | 5 seconds | Minimum elapsed time between batch decisions, from the sampling interval to 1 hour |
| `HeatingTimeConstant` | 30 seconds | Controls the approach to operating temperature |
| `CoolingTimeConstant` | 45 seconds | Controls the approach to ambient temperature |
| `Profiles` | `CNC-001`, `CNC-002`, `CNC-003` | One to 32 machine profiles |

Both thermal time constants must be positive and at most 1 hour. Profiles specify a unique nonempty GUID (`Id`), a unique canonical machine `Code`, initial `Status` (`Running`, `Idle`, or `Offline`), `TargetRpm`, `AmbientTemperature` and `OperatingTemperature` in °C, `RunningVibration` in mm/s, `RatedTorque` in N·m, and `ToolWearPerHour` as a fraction from 0 to 1. A code can contain uppercase ASCII letters, digits, hyphens, and underscores.

## Behavior and persistence

- `CNC-001` and `CNC-002` start running with different profiles. Smoothed load drives correlated changes in RPM, vibration and torque. Temperature approaches the operating target, and tool wear increases toward its upper bound.
- `CNC-003` starts idle. Idle and offline profiles report zero RPM, vibration, and torque. They cool toward ambient temperature and retain their current wear.
- Startup reuses rows by machine code and preserves their database IDs. New rows use the configured IDs. The profile's status is applied at initialization. Each process start begins the simulation at ambient temperature with zero wear and a fresh seeded stream.
- The worker keeps one reading per machine in memory. It writes the latest batch on the first sample at or after the persistence interval, using one `SaveChangesAsync` call. The timer coalesces missed ticks when work takes longer than one interval.
- Cancellation stops the timer and releases the active service scope. Initialization and persistence failures are logged and propagated. SQLite retains written history independently of the bounded sampling memory.

The model advances by the configured sampling step for each generated reading. Matching the configuration, seed, initial state, call sequence, and UTC clock values reproduces the readings within the same runtime.
