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

**My reasons:** I wanted another agent's point of view and insight rather than relying on one. I'll also have this agent write a full prompt for Codex on the whole assignment as well.

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
- **Overrode:** the AI suggested `CLAUDE.md` as the steering file; I chose `AGENTS.md`. Reason: it's agent-agnostic. Claude Code now reads both files, but `AGENTS.md` is the cross-tool industry standard, so any agent (Codex included) picks up the same rules.
- **Note:** the workflow skills can't be invoked by the agent (they are marked user-invoked only), so I run each phase command myself. The agent wrote `AGENTS.md` so the working rules survive each `/clear`.

## Entry #3 (Thu 29 Oct 10:56): skills setup

- **Directed:** after the AI posted its recommendations on the open design questions, I told it to stop suggesting or recommending anything until I drive the next phase. Those recommendations stay unconfirmed until `/grill-with-docs`.
- **Asked:** `/ask-matt` to check where I am in the flow, then `/setup-matt-pocock-skills`.
- **AI proposed:** the setup skill defaults to GitHub Issues because the remote is on GitHub. The AI flagged that I had already chosen local markdown in entry #2 and kept that. It recommended the default triage labels and the single-context domain-docs layout (one `GLOSSARY.md` and `docs/adr/`).
- **Accepted:** local markdown tracker (`.scratch/<feature>/`), default labels, single-context layout. Written to `docs/agents/` plus an `## Agent skills` block in `AGENTS.md`.

## Entry #4 (Thu 29 Oct 11:02): design grilling (`/grill-with-docs`)

- **Asked:** grill me on the design in rounds, one decision at a time, and write down the glossary and ADRs as decisions settle.
- **AI proposed:** 31 decisions over three rounds, each with a recommended answer. They're logged in [docs/decisions.md](docs/decisions.md) with what each was chosen over.
- **Accepted:** all 31 recommendations. Two of them change my original prompt:
  - Build the transactional outbox now instead of "only if time allows" (D15, ADR-0004).
  - Don't allow rejecting a claim straight from Submitted (D5).
- **AI challenged its own earlier proposal:** in round 3 it revisited ADR-0001 and replaced the Claims / Assessment / Reporting modules with Claims / Notifications / Reporting, because claimant and officer actions change the same Claim (D21).
- **Directed:**
  - Every ADR and decision must say what it was chosen over and why. The AI added "Considered Options" to the ADRs and wrote the decision log.
  - The decisions must line up with the brief. The brief has no scored rubric, so the AI mapped each of its "questions to consider" to the decision that answers it (top of docs/decisions.md).
- **Written:** `GLOSSARY.md`, ADRs 0001–0006, `docs/decisions.md`.

## Entry #5 (Thu 29 Oct 11:05): event list, and which skills to use

- **AI caught a gap in my prompt:** none of the six events carried a Reserve change, so the read model couldn't calculate Exposure. It proposed adding `ReserveSet` and splitting `ClaimDecided` into `ClaimApproved` and `ClaimRejected` (D32). Accepted.
- **Asked:** should I use `/wayfinder`, `/codebase-design` or `/domain-modeling` for the assignment as a whole? AI recommended: no `/wayfinder` (it's for efforts too big and foggy for one session, and the design cleared in one grilling); no separate `/codebase-design` session (`/tdd` pulls it in as a reference when a module's interface is in question); and keep `/domain-modeling` running, since `/grill-with-docs` already uses it. My decision: see entry #8.
- Corrected entry #4's time: the AI had written 11:03 before running `date`, which said 11:02.

## Entry #6 (Thu 29 Oct 11:09): spec (`/to-spec`)

- **Confirmed:** I told the AI we have a shared understanding of the design and of what the brief asks for, which closed the grilling.
- **AI proposed three test seams:** the HTTP API (real Postgres), the Claim's public methods (plain unit tests), and Reporting's apply-event method (no Kafka). The outbox worker, Kafka adapters, Notifications and Angular are checked by hand. **Accepted.**
- **AI wrote** `.scratch/claims-platform/spec.md` (64 user stories, REST contract, events, read model, tests, out of scope).
- **AI flagged gaps it filled itself** (in the spec's Further Notes) for me to confirm or override: reject needs a reason; missing identity headers return 401; an Officer acting on another Officer's Claim gets 403; Reporting stores current state per Claim rather than running totals; Notifications skip ReserveSet and InfoProvided. **Accepted all five.**

## Entry #7 (Thu 29 Oct 11:12): tickets (`/to-tickets`)

- **AI proposed** 12 tickets in `.scratch/claims-platform/issues/`, each with its blocking tickets:
  - Backend slices first (01–07): skeleton, submit and track, Queue and pick-up, assess and decide, Info Requests, outbox → Kafka → notifications, Workload and Exposure read model.
  - Then Angular (08–11), with the dashboard cut first if time runs short.
  - Then README and the fresh-clone check (12).
- **AI flagged:**
  - It departed from the skill's rule that each slice includes the UI, following the brief's "start with the backend".
  - It merged the outbox publisher with the Notifications consumer so ticket 06 can be shown working end to end.
  - Tickets 04 and 05 could run in parallel, but both change the Claim, so running them one after the other is safer.
- **Accepted** the breakdown as proposed.

## Entry #8 (Thu 29 Oct 11:24): GitHub Issues and a wayfinder map

- **Correction:** entries #2 and #3 record me as accepting a local markdown tracker. I hadn't chosen that. The AI carried it over from an earlier note and declared it "settled" during setup. The spec and tickets were wrongly written to `.scratch/` as a result.
- **Overrode** (tracker): everything lives on GitHub, not locally, because the panel will inspect my GitHub repo.
- **Overrode** (wayfinder): the AI had advised against `/wayfinder` because all decisions were settled in one grilling session. I chose it anyway as the overall tracker. Reason: it gives the whole assignment one source of truth that points to everything else, tracked through sub-issues.
- **AI then built:**
  - **#1** is the wayfinder map, holding the destination, notes, decisions so far (linked to the ADRs and decision log), not-yet-specified items and out of scope.
  - **#2** is the spec, a sub-issue of the map.
  - **#3–#14** are the 12 tickets, sub-issues of the spec, with GitHub's native "blocked by" links and the `ready-for-agent` label.
  - The tracker config was switched to GitHub and the `.scratch/` copies were deleted, so GitHub is the only source of truth.
- **AI flagged a deviation from the skill:** wayfinder maps normally hold only decision tickets. Here the map also parents the spec and its build tickets, because I wanted one tracker for the whole assignment. Future decisions (for example the Codex review) become `wayfinder:*` tickets under #1.

## Entry #9 (Thu 29 Oct 11:27): outbox reason; repo made public

- **Correction:** the line in my opening prompt saying to build the outbox "only if time allows" was drafted by the earlier AI, not by me.
- **My reason for building the outbox now (D15):** when the AI recommended it, I saw no reason not to just go ahead and build it.
- **Repo made public** at my request. Before switching, the AI checked the code, docs and all 14 issues for client names and secrets (none found) and confirmed that only `main` is on GitHub.
