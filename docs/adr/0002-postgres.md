# Postgres with EF Core, not SQL Server

We use Postgres through EF Core rather than SQL Server, even though SQL Server is the author's strongest database. Postgres runs natively on Apple Silicon, has a small image and starts in seconds, so a fresh clone comes up first time. EF Core keeps the data access code close to what it would be on SQL Server, so switching later is mostly a provider change.

## Considered Options

- **SQL Server**: rejected. It runs emulated on Apple Silicon, the image is about 2GB and it starts slowly, which puts the "runs locally in a few commands" deliverable at risk.
- **SQLite or the EF in-memory provider**: rejected. Neither behaves like a production database for concurrency, and the pick-up conflict check relies on a real row version (Postgres `xmin`).
