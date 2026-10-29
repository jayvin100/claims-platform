# AI Working Journal

A running log of what I asked the AI, what it proposed, and what I accepted, challenged or overrode.
Times come from `date` on my machine (+08). The full opening prompt is in [docs/agent-prompt.md](docs/agent-prompt.md).

## Time log

| Start | End | What |
|-------|-----|------|
| Thu 29 Oct 10:48 | | Build session starts. Toolchain check, repo skeleton, design confirmation |

---

## Entry #0 (before the build session): brief analysis and prompt hardening

- Used AI to analyse the brief: it identified the real assessment criteria (decisions, prioritisation, AI direction, defensibility) and suggested a modular monolith, REST for commands/queries, Kafka for domain events + projections, the outbox pattern, decimal+currency money, ownership checks, optimistic concurrency, and a time plan.
- A second AI drafted the initial agent prompt. I had the first AI critique it. Changes I accepted: fewer "continue?" stops (time cost); resolve the DB contradiction; add fake-identity ownership enforcement; add a set-reserve action (otherwise exposure is always zero); make exposure read from the Kafka projection so Kafka isn't decorative; group exposure by market + currency; add optimistic concurrency, idempotent consumers, and IEventPublisher for testability; use real `date` for the time log; mention NSwag as a .NET-friendly client generator.
- Created the private GitHub repo and cloned it locally.

**My reasons:** _(to be filled in by Jay)_

---

## Entry #1 (Thu 29 Oct 10:48): toolchain check and skeleton

- **Asked:** check the toolchain, create the repo skeleton, then present the design with a recommendation on each open question.
- **AI found:** .NET SDK 10.0.401 only (no .NET 8 SDK); Node 26.13 / npm 11.20; no global Angular CLI (Angular 22.2 is current and supports Node 26); Docker 29.9 + Compose 5.6 on arm64.
- **AI proposed:** target .NET 10 (current LTS) instead of .NET 8, and run the Angular CLI through `npx` rather than a global install. Recommendations on the open design questions are in entry #2.
- **Decision:** _(pending Jay)_
