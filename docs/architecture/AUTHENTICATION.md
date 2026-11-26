# Authentication and access control

ASP.NET Core Identity is the authentication authority. It uses the existing EF Core SQLite context and default password hasher. The `MonitoringIdentity` migration creates the Identity tables independently of telemetry and alarms. The application requires a 12-character minimum password, uses the framework composition rules, and locks accounts for 15 minutes after five failures. Login is also limited to ten requests per minute per remote address. Failed sign-ins use the same response for invalid credentials, unknown accounts, locked accounts, and disabled accounts.

## Roles and endpoints

| Role | Monitoring REST and live readings | Alarm acknowledgement | Account activation |
| --- | --- | --- | --- |
| Viewer | Allowed | Forbidden | Forbidden |
| Operator | Allowed | Allowed | Forbidden |
| Administrator | Allowed | Allowed | Allowed for other accounts |

Administrative account provisioning is a host operation. The application exposes no registration, password-reset delivery, or user-management UI. `GET /health` is anonymous process liveness. Machine, reading, alarm, and application status endpoints require a monitoring role.

| Method and path | Result |
| --- | --- |
| `GET /api/auth/csrf` | Issues the antiforgery cookie pair |
| `POST /api/auth/login` | Accepts username and password, issues the session cookie, and returns the role and capability summary |
| `GET /api/auth/me` | Returns the current authenticated user's name, roles, capabilities, and expiry |
| `POST /api/auth/logout` | Revokes the user's sessions and removes the browser cookie |
| `PUT /api/admin/users/{userName}/activation` | Sets another account's activation using a JSON `isActive` boolean |

API endpoints use `401` for invalid or missing sessions and `403` for insufficient permissions. Login returns `429` when its rate limit is exceeded. Missing or invalid antiforgery tokens return `400`. The Angular client reads the `XSRF-TOKEN` cookie issued for the current principal and sends it in `X-XSRF-TOKEN` on same-origin writes. The framework validates it against its separate HttpOnly antiforgery cookie. Clients fetch a new request token after a principal change and before subsequent writes.

## Sessions and the monitoring hub

The session cookie is host-only, HttpOnly, Secure, and SameSite Strict. `Access:SessionMinutes` defaults to 30 and accepts 5–120 minutes. Expiry is absolute. Identity checks the security stamp and active flag on authenticated HTTP requests. Logout rotates the stamp, clears the cookie, and disconnects the user's live monitoring connections in the API process. An activation change through the admin API also rotates the target account's stamp and ends its connections. A connection closes at ticket expiry. If an account is disabled outside the running API, an existing WebSocket can remain connected until its ticket expires.

Development and Testing offer `/hubs/monitoring` with the same cookie and monitoring policies as REST. Production requires both `RealtimeMonitoring__Enabled=true` and `RealtimeMonitoring__AllowProduction=true`. The second setting defaults to false. Staging and other environments leave the endpoint unavailable. An enabled endpoint rejects anonymous callers. The existing origin checks and read-only hub contract apply. Connection expiry and client logout close the Angular connection and clear its monitoring state.

## Provision and sign in locally

From the repository root in PowerShell, apply the migrations before provisioning. Use the same `Database__Path` for the command and API when overriding the default. The command accepts a password on stdin and exits before starting any hosted services.

```powershell
dotnet restore FactoryPulseAI.slnx
dotnet build FactoryPulseAI.slnx --no-restore
dotnet tool restore
dotnet ef database update --project src/FactoryPulse.Infrastructure --startup-project src/FactoryPulse.Api --context FactoryPulseDbContext
Read-Host 'Administrator password' -MaskInput |
    dotnet run --project src/FactoryPulse.Api --no-build --no-launch-profile -- provision-user local-admin Administrator
dotnet dev-certs https --trust
$env:Simulation__Enabled = 'true'
dotnet run --project src/FactoryPulse.Api --no-build --launch-profile "FactoryPulse.Api (HTTPS)"
```

In another terminal, run `npm ci` and `npm start` in `src/FactoryPulse.Web`, then open `http://localhost:4200` and sign in. Use loopback localhost for development and a single HTTPS origin for deployment. The development proxy connects to the API over verified HTTPS. Keep production frontend and API traffic on the same HTTPS origin.

For a deployment, provision accounts through a privileged host shell or a secret provider that supplies stdin. Preserve and protect the ASP.NET Core Data Protection key ring across restarts. Configure the expected hosts and trust only known infrastructure when forwarding client IP addresses. Without client address forwarding, users behind the same proxy share its login rate-limit bucket. Start with one API instance. For a future read-only demo, provision a dedicated Viewer account and manage its credentials outside source control.

## Verification

Run `dotnet test --solution FactoryPulseAI.slnx --no-build` from the repository root and `npm test` and `npm run e2e` from the web project. The optional real-backend Playwright test uses an isolated account and reads `FACTORYPULSE_TEST_USERNAME` and `FACTORYPULSE_TEST_PASSWORD` from its environment. Set `FACTORYPULSE_WEB_LIVE` to `development` for live readings or `production` for REST fallback with simulation and the production hub disabled.
