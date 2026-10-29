# Claims Platform

A claims platform for motor and property claims across six APAC markets. One Angular app serves claimants and claims staff; an ASP.NET Core API is the backend, with REST for request/response work and Kafka for domain events.

> Work in progress. Setup steps, design decisions, what was left out and what's next will be filled in as the build progresses.

## Run locally

Needs Docker, and the .NET 10 SDK.

```sh
docker compose up -d --wait     # Postgres (host port 5433) and Kafka (9092)
dotnet tool restore             # local dotnet-ef
dotnet run --project src/Claims.Api   # migrates, seeds demo users, serves http://localhost:5169
```

- Swagger UI: http://localhost:5169/swagger. Call `GET /api/users`, then use **Authorize** to enter a user's id as `X-User-Id` and their role as `X-Role`.
- Tests: `dotnet test` (needs the Compose Postgres; uses its own `claims_test` database).

## Repo layout

| Path | What |
|------|------|
| `AI_JOURNAL.md` | Log of how AI was directed, challenged and overridden during the build |
| `docs/agent-prompt.md` | The opening prompt given to the AI pair-programmer |
| `docs/decisions.md`, `docs/adr/` | Decision log and architecture decision records |
| `src/Claims.Api` | The API: `Claims/`, `Notifications/`, `Reporting/` modules plus shared `Infrastructure/` |
| `tests/Claims.Api.Tests` | Integration tests (and, later, unit tests) |
