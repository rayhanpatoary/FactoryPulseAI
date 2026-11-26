# Web monitoring dashboard

The Angular client in `src/FactoryPulse.Web` shows simulated machine status, live telemetry, and persisted alarm and measurement history. The dashboard, machine catalogue and detail, alarms, history, and settings are routable views. The shell keeps connection status and language controls visible across those views.

## Local development

Use Node.js 24, npm, and the .NET SDK pinned in `global.json`. Follow these commands in PowerShell from the repository root. If using the workspace-local Windows SDK, substitute `./.dotnet/dotnet.exe` for `dotnet`. The first terminal starts the API and sample generator. Omit the simulation override or set it to `false` to run with the stored machine data alone.

```powershell
dotnet restore FactoryPulseAI.slnx
dotnet build FactoryPulseAI.slnx --no-restore
dotnet tool restore
dotnet ef database update --project src/FactoryPulse.Infrastructure --startup-project src/FactoryPulse.Api --context FactoryPulseDbContext
dotnet dev-certs https --trust
$env:Simulation__Enabled = 'true'
dotnet run --project src/FactoryPulse.Api --launch-profile "FactoryPulse.Api (HTTPS)" --no-build
```

In a second terminal, start Angular and open `http://localhost:4200`. Node uses the operating system certificate store for the local development proxy. If you manage trust per process, set `NODE_EXTRA_CA_CERTS` to a PEM containing the trusted public development certificate before running `npm start`.

```powershell
Set-Location src/FactoryPulse.Web
npm ci
npm start
```

The proxy forwards relative `/api` and `/hubs` requests to `https://localhost:7161` and supports WebSocket upgrades. It translates the browser Origin only for the expected loopback Angular host and origin. The API retains its same-origin validation. TLS verification remains enabled.

## Client architecture

Standalone, lazy-loaded routes use Angular Material controls and a shared responsive shell. Typed REST calls use `HttpClient`. Request streams use RxJS cancellation when a route or filter changes. The dashboard uses the catalogue, each machine's latest reading, and a paginated active alarm summary. The History view requests one persisted page at a time and converts local date input to the API's inclusive UTC lower bound and exclusive upper bound.

The SignalR client registers the monitoring handlers before starting its connection. The monitoring store merges the latest samples per machine by UTC timestamp, including fractional precision. After reconnecting, it refreshes REST snapshots and retains readings that are newer than the stored response. Alarm events trigger a refresh of authoritative REST data. Machine detail combines a REST page with a maximum of 120 unique recent samples and renders three Chart.js views. The history table remains available as a text alternative.

Runtime English and German dictionaries are provided through ngx-translate. Numbers, times, and chart labels use the selected locale. The client stores only the language preference and leaves machine codes and alarm codes intact.

## Deployment settings

The production client build is written to `src/FactoryPulse.Web/dist/browser`. A host must serve SPA routes from `index.html` and forward the API paths on the same origin. Live updates remain restricted to Development and Testing. In environments where the hub is unavailable, REST views continue to work and the shell shows the connection state.

`/web-config.json` defaults to `{"allowAlarmAcknowledgement": false}`. An explicitly authorized Development or Testing instance can serve the same file with that value set to `true`. The API still authorizes each acknowledgement. A `403` disables the control for that browser session. Keep the option disabled for public deployments, and keep configuration and files served to the browser free of secrets.

## Verification

From `src/FactoryPulse.Web`, run the following. The default Playwright suite uses deterministic API responses and starts its own frontend server when one is not already running.

```powershell
npm ci
npm run lint
npm run format:check
npm run build
npm test
npx --no-install playwright install chromium
npm run e2e
```

To verify the real local integration, start both services as above and run `$env:FACTORYPULSE_WEB_LIVE = 'development'` followed by `npm run e2e`. The live test checks an actual WebSocket reading, the rendered machine timestamp, and the machine chart route. With a migrated empty database and a Production API with simulation disabled, use `FACTORYPULSE_WEB_LIVE=production` to check fallback behavior. Clear the variable before running the fixture suite again.

[Monitoring REST contracts](MONITORING-API.md) · [Alarm contracts](ALARMS.md) · [Real-time delivery](REALTIME.md)
