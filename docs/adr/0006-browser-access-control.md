# ADR 0006 — Use same-origin Identity cookie sessions

**Status:** Accepted

The browser client needs authenticated REST and SignalR access. ASP.NET Core Identity stores users and roles in the existing SQLite database. The API issues Secure browser cookies with a fixed lifetime, checks antiforgery tokens for mutations, and applies role policies at the server boundary. The Angular client keeps session state in memory.

Account provisioning runs as an explicit host command. Production SignalR requires opt-in and a monitoring session. Enterprise OpenID Connect and native client flows can be added at the authentication boundary. [Access contracts and deployment](../architecture/AUTHENTICATION.md)
