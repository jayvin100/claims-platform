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
- **Decision:** `npx` for Angular accepted. .NET 10 is to be confirmed during grilling (entry #2).
- Also fixed: the first commit had the auto-generated hostname email as its author. Set the repo's git identity to my name and email, amended that commit and force-pushed it (the only commit at that point).

## Entry #2 (Thu 29 Oct 10:52): choosing the agentic workflow

- **Asked:** `/ask-matt`, to pick a workflow from the Matt Pocock skills that shows how I direct agents.
- **AI proposed:** setup → `/grill-with-docs` (ASK items become ADRs) → `/to-spec` → `/to-tickets` (local markdown in `.scratch/`) → `/implement` per ticket with `/clear` between tickets (each one runs `/tdd` and `/code-review`) → `/implement-spec` with parallel subagents only where the ticket graph forks → `/retro`. It flagged two consequences: the build order becomes vertical slices (backend slices first, Angular last), and the process adds about 45 minutes of overhead.
- **Accepted:** the flow, vertical slices, and the local tracker.
- **Overrode:** the AI suggested `CLAUDE.md` as the steering file; I chose `AGENTS.md`. Reason: _(pending Jay)_
- **Note:** the workflow skills can't be invoked by the agent (they are marked user-invoked only), so I run each phase command myself. The agent wrote `AGENTS.md` so the working rules survive each `/clear`.

## Entry #3 (Thu 29 Oct 10:56): skills setup

- **Directed:** after the AI posted its recommendations on the open design questions, I told it to stop suggesting or recommending anything until I drive the next phase. Those recommendations stay unconfirmed until `/grill-with-docs`.
- **Asked:** `/ask-matt` to check where I am in the flow, then `/setup-matt-pocock-skills`.
- **AI proposed:** the setup skill defaults to GitHub Issues because the remote is on GitHub. The AI flagged that I had already chosen local markdown in entry #2 and kept that. It recommended the default triage labels and the single-context domain-docs layout (one `GLOSSARY.md` and `docs/adr/`).
- **Accepted:** local markdown tracker (`.scratch/<feature>/`), default labels, single-context layout. Written to `docs/agents/` plus an `## Agent skills` block in `AGENTS.md`.
