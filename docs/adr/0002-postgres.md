# Postgres with EF Core, not SQL Server

We use Postgres through EF Core rather than SQL Server, even though SQL Server is the author's strongest database. Postgres has native arm64 images for Apple Silicon and a small footprint, which keeps a fresh local start simple. EF Core keeps most data access code close to what it would be on SQL Server, though migrations, the Claim Number sequence and the `xmin` concurrency token are provider-specific and would need rework on a switch.

## Considered Options

- **SQL Server**: rejected. Microsoft supports its Linux containers on x86-64 hosts only, so on Apple Silicon it runs under emulation, which is unsupported, and the image is about 2.3GB. That puts the "runs locally in a few commands" deliverable at risk. (Startup speed was not measured.)
- **SQLite or the EF in-memory provider**: rejected. Neither behaves like a production database for concurrency, and the pick-up conflict check relies on a real row version (Postgres `xmin`).
