# 01: Runnable skeleton with demo identity

**What to build:** A developer can start Postgres and Kafka with one Docker Compose command, run the API, and see it apply migrations and seed the demo users. Swagger UI loads, `GET /api/users` lists the 2 Claimants and 3 Officers, and any other request without valid identity headers is refused. The test project runs and can talk to a dedicated test database. This sets up the modules (Claims, Notifications, Reporting) as folders in one API project, per ADR-0001.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Docker Compose starts Postgres and Kafka (KRaft, `apache/kafka`), both native arm64
- [ ] The API targets .NET 10, uses controllers, and has Claims, Notifications and Reporting module folders plus shared infrastructure
- [ ] On startup in Development, the API applies EF migrations and seeds 2 Claimants and 3 Officers with fixed ids
- [ ] `GET /api/users` returns the seeded users (marked demo-only in Swagger)
- [ ] Every other endpoint requires `X-User-Id` and `X-Role`; a missing header, or one that doesn't match a seeded user, returns 401
- [ ] The current user is read in one place, so swapping headers for JWT claims later is a one-place change (ADR-0006)
- [ ] Swagger UI is served in Development from the generated OpenAPI document
- [ ] The test project runs integration tests through `WebApplicationFactory` against a separate test database on the Compose Postgres
- [ ] An integration test proves the 401 rule
- [ ] Every NuGet package is confirmed with Jay before it's added
