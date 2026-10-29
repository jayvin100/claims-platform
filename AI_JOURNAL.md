# AI Working Journal

A running log of what I asked the AI, what it proposed, and what I accepted, challenged or overrode.
Times come from `date` on my machine (+08). The full opening prompt is in [docs/agent-prompt.md](docs/agent-prompt.md).

## Time log

| Start | End | What |
|-------|-----|------|
| Thu 29 Oct 10:48 | 10:56 | Toolchain check, repo skeleton, workflow choice, skills setup |
| 10:56 | 11:12 | Design grilling (32 decisions), spec, tickets |
| 11:12 | 11:35 | Move tracking to GitHub Issues under a wayfinder map; repo made public; Codex review prompt |
| 11:35 | 11:53 | Codex runs its independent review (clock kept running) |
| 11:53 | 12:01 | Weigh the Codex findings, apply decisions to spec, tickets, ADRs |
| 12:04 | 12:17 | Ticket 01 (#3): Compose, API skeleton, demo identity, tests, code review |
| 12:20 | 12:30 | Ticket 02 (#4): Claim entity, submit/list/detail endpoints, outbox table, tests, code review |
| 12:32 | 12:39 | Ticket 03 (#5): Queue, pick-up, `xmin` concurrency, 409 mapping, tests, code review |

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

## Entry #10 (Thu 29 Oct 11:35): cross-agent review by Codex

- **Asked:** write a full prompt and context for Codex to evaluate the whole assignment, saved to my Downloads folder.
- **AI wrote** `~/Downloads/codex-review-prompt.md`:
  - Codex is read-only and writes its review to `~/Downloads/codex-review.md`.
  - It must sketch its own design from the brief **before** reading our plan, so it doesn't just agree.
  - It then gives a verdict on each of D1–D32 and the ADRs, checks the spec and tickets, flags technical risks on this machine (marked verified or from memory), proposes a cut list, writes the hardest panel questions, and gives honest feedback on this journal, including whether my acceptances look like rubber-stamping.
- **Tracked** as wayfinder ticket #15 (`wayfinder:task`, claimed by me) under the map #1. It graduated out of the map's "Not yet specified" section.

## Entry #11 (Thu 29 Oct 12:01): weighing the Codex review

- **Codex's verdict:** keep the architecture. Stop adding planning. It found real gaps in the event contract and startup, and flagged that my grilling acceptances (31 of 31) and my outbox reason looked like rubber-stamping. It also corrected the clock: on a continuous count the 5-hour cap ends at **15:48 today**, not tomorrow.
- **The AI put six decisions to me**, giving its own view where it differed from Codex:
  - **Outbox:** Codex said build it only within a 15–20 minute gate; the AI said keep it. **I chose to build it.** _(Reason beyond "yes": pending Jay.)_
  - **Exposure:** rename the figure to Open Reserves and add an Unreserved count, so unpriced claims don't show as zero. **Accepted.**
  - **Cuts:** Codex proposed an ordered cut list. **Overrode: no cuts.** My reason: we can make it in time.
  - **Time budget:** Codex proposed 120 more minutes with checkpoints. **Adopted as the ceiling.** I expect to finish under it.
  - **No-trade-off fixes:** Claimant id on ClaimPickedUp, outbox sequence ordering with a single publisher, per-consumer dedup, offsets committed after processing, background topic retry, all Kafka off in tests, a two-context `xmin` test, Swagger and CORS exempt from identity, and corrected ticket links. **Accepted.** My reason: a no-brainer.
  - **Dependencies:** pinned (Npgsql EF 10.0.3, Confluent.Kafka 2.16.0, Angular CLI 22.2.5, local `dotnet-ef`); the rest at the latest stable version, recorded when added. **Accepted.** My reason: if anything breaks later, it won't be because a dependency version changed.
- **Applied:** spec #2 and tickets #3, #4, #5, #7, #8, #9 and #13 updated, plus links on #11 and #14. Glossary gains Open Reserves and Unreserved Claim. ADR-0002, 0004 and 0005 are qualified where Codex showed they promised too much. Decision log D33–D37 added. The time log is now a running record.
- **The AI noted where it disagreed with Codex:** on the outbox, because without one the request must call Kafka, which needs a fake publisher in tests and couples REST to the broker.

## Entry #12 (Thu 29 Oct 12:17): ticket 01, runnable skeleton (#3, `/implement`)

- **Asked:** implement #3 test-first, then review it, commit and push. Mid-ticket I told the AI to note every change, decision and question in the docs.
- **AI asked me, and I chose:**
  - **Packages:** approved all at the latest stable versions (D38), and dropped coverlet.collector, which the xunit template adds but wasn't approved.
  - **xunit 2.9.3 over xunit.v3** (D39). The AI recommended it as the .NET 10 template default with the most `WebApplicationFactory` examples.
  - **EF version warning:** the tests ran EF 10.0.4 while the API ran 10.0.14. I chose to pin EF Core Relational 10.0.14 in the API (D40) over lowering EF Core Design to 10.0.4 or leaving the warning.
- **AI found and fixed, without asking** (D41–D46; they don't change an agreed decision, but are mine to override):
  - A Homebrew Postgres on my machine owns `localhost:5432` and answered instead of the container ("role claims does not exist"). The AI moved Compose Postgres to host port **5433** rather than stopping my local service.
  - Images: `postgres:18.6-alpine` and `apache/kafka:4.3.2`, both checked as native arm64; it skipped Kafka 4.4.0 as only days old.
  - Identity as an ASP.NET Core authentication scheme plus a "must be signed in" fallback policy, so the JWT swap is one line in `Program.cs`. Seeding through EF `HasData` in the migration, so a second start can't duplicate users (checked: two starts, still 5 users).
  - A test-only `/test/whoami` endpoint for the 401 tests, instead of adding an endpoint that isn't in the REST contract.
  - A Swagger "Authorize" button for the two headers, and CORS for `http://localhost:4200`.
- **TDD note:** the users-endpoint, missing-headers and exemption tests were written red first. The five "headers don't match a seeded user" cases passed on their first run, because the handler written for the missing-headers test already covered them.
- **Code review (`/code-review`, two parallel sub-agents, standards and spec):**
  - **Fixed:**
    - Swagger showed a lock on `GET /api/users`.
    - The EF Relational pin wasn't explained.
    - A test assertion hid the actual status code on failure.
    - The two header names were listed twice.
    - "user" is on the glossary's avoid list, so **Demo User** was added to `GLOSSARY.md`.
  - **Rejected:** the spec reviewer said an unknown route without headers returns 404. The AI had already seen 401 from the running API, and added a test that proves it.
  - **Left as is:** the test connection string repeats the dev one (explicit is easier to read), and roles go out as strings.
- **Result:** 12 integration tests pass against `claims_test`, with 0 build warnings. Build time at commit: 1h29m of the 5h cap.
- **Questions for me:** _(pending Jay)_ do I want to override any of D41–D46?

## Entry #13 (Thu 29 Oct 12:30): ticket 02, Claimant submits and tracks a Claim (#4, `/implement`)

- **Asked:** `/implement 4`: test-first, review, commit and push.
- **AI built, test-first at the two agreed seams:**
  - **Seam 2 (the Claim, no database), 12 tests:** submit gives a Submitted Claim with `CLM-<market>-<6 digits>`; the currency is set from each of the six Markets; submit records a Claim History entry and raises ClaimSubmitted with all its fields; a future incident date is refused; today is accepted, judged on the Market's own clock.
  - **Seam 1 (HTTP, real Postgres), 18 tests:**
    - `POST /api/claims` returns 201; an Officer submitting gets 403.
    - Missing or invalid fields return 400 (seven cases); a future incident date returns 400 with the Claim's message.
    - A currency sent by the Claimant is ignored.
    - `GET /api/claims` lists only the caller's Claims, newest first; an Officer listing gets 403.
    - `GET /api/claims/{id}` returns the Claim with its Claim History; another Claimant's Claim and an unknown id get 404; an Officer can open any Claim.
    - The ClaimSubmitted outbox row is written with its sequence number, type, Claim id and full JSON payload.
- **AI's implementation calls, for me to review** (D47–D53): today judged by the Market's capital time zone; input limits and enum names only; a refused submit can leave a gap in Claim Numbers; events go to the outbox from one `SaveChanges` override; one exception handler maps Claim errors to problem details; Reserve and Assigned Officer are left for their own tickets; the list returns a summary shape.
- **TDD note:** six of the seven bad-input cases and the Officer 403s passed on their first run, because `[ApiController]` validation and the role attribute already handled them. The future-date 400 was red (a 500) until the exception handler was added.
- **AI caught one possible flaky test:** Postgres keeps microseconds while .NET keeps 100 ns ticks. macOS happens to tick in microseconds, so the test passed here but could fail on Linux. The one comparison against a database time now allows 1 µs.
- **Code review (`/code-review`, two parallel sub-agents, standards and spec):**
  - **Fixed:**
    - Events were cleared before the save succeeded; they are now cleared after it.
    - The two `switch`es on Market became one table.
    - The `"Value"` alias in the sequence query is now explained.
    - The Claim's summary now cites D32 for events.
    - The sync `SaveChanges` override and the stored currency are now explained.
    - `InvalidClaimException` now says that state rules will use a separate 409 error.
  - **Left as is:**
    - The event records repeat the five common fields, because C# positional records need them.
    - The tests name the seeded users explicitly, which is easier to read than looking them up.
    - `ClaimType` and `ClaimStatus` share one file.
- **Smoke-checked** against the dev database: `CLM-ID-000001` was created with IDR; a future date returned a 400 problem-details response; Swagger lists the three endpoints.
- **Result:** 42 tests pass (12 identity, 12 Claim, 18 HTTP), with 0 build warnings. Build time at commit: 1h42m of the 5h cap.
- **My decision (12:31):** accepted D47–D53 as proposed, with no overrides. My reason: I agree with the AI's reasoning on each one, and it makes sense.

## Entry #14 (Thu 29 Oct 12:39): ticket 03, Officer Queue and pick-up (#5, `/implement`)

- **Asked:** `/implement 5`: test-first, review, commit and push.
- **AI built, test-first at the agreed seams:**
  - **Seam 2 (the Claim, no database), 7 new tests:** pick-up makes the Officer the Assigned Officer and moves a Submitted Claim to Under Review, with a Claim History entry and ClaimPickedUp (Assigned Officer id and Claimant id); a second pick-up is refused and changes nothing; pick-up is refused from each of the five other statuses.
  - **Below HTTP, 1 test:** two database contexts load the same Submitted Claim, both pick it up, the second save throws a concurrency conflict and the first Officer keeps it. It was red first (the second save silently overwrote) until `xmin` was mapped.
  - **Seam 1 (HTTP), 9 tests:** pick-up returns the updated Claim; a second pick-up is 409 naming the rule; two Officers at once get one 200 and one 409 (stable over 10 runs); an unknown Claim is 404; ClaimPickedUp is in the outbox with both ids; a Claimant on either Officer endpoint gets 403; the Queue has unassigned and own Claims oldest first, with "is mine", and leaves out other Officers' Claims.
- **AI's implementation calls, for me to review** (D54–D59): `xmin` as a shadow row version; one handler maps state rules and concurrency conflicts to 409; the Queue row shape; the Queue filter; setting status by reflection in one unit test; 404 for an unknown Claim and a shared test-users helper.
- **TDD note:** the refusal tests, the race, the outbox row, the 404 and the pick-up 403 passed on their first run, because the code from the earlier red tests already covered them.
- **AI slip, fixed:** it generated a migration, applied it to the test database by running tests, then removed and regenerated it. The test database kept the old column. The AI dropped it and the stale history row from `claims_test` by hand; the dev database was never touched.
- **Code review (`/code-review`, two parallel sub-agents, standards and spec):**
  - **Fixed:** the test identity record is now named `DemoUser`, the glossary term; a one-line forwarder (`As`) in the older tests was removed.
  - **Left as is:**
    - The race test usually fails the loser on the status rule, not `xmin`; the two-context test proves `xmin` on its own, as the spec asks.
    - The definition of "open" (neither Settled nor Rejected) is only in the Queue query so far; Workload reads events, not this table.
    - The Queue row repeats most of the My Claims row; they serve different screens.
    - Some test setup (the submit body) repeats across test files.
  - **Known gap:** no test yet shows the Queue leaving out my Settled or Rejected Claims, because no endpoint reaches those statuses. It belongs in #7.
- **Result:** 59 tests pass (12 identity, 19 Claim, 1 concurrency, 18 submit/track, 9 Queue/pick-up), with 0 build warnings. Build time at commit: 1h51m of the 5h cap.
- **Questions for me:** _(pending Jay)_ do I want to override any of D54–D59?

