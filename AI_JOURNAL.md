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
| 12:39 | 12:44 | Second review of #5 by two Claude agents plus Codex; Codex review made a standing rule |
| 12:48 | 12:58 | Ticket 04 (#6): Reserve, approve, reject, settle; tests; review by two Claude agents plus Codex |
| 12:58 | 13:00 | Plan parallel waves for the remaining tickets; per-worktree databases; fix a test start-up race |
| 13:00 | | Wave 1 running in parallel: #7, #8, #10, each in its own worktree |

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
  - **Known gap:** no test yet shows the Queue leaving out my Settled or Rejected Claims, because no endpoint reaches those statuses. It belongs in #6 (approve, reject, settle).
- **Result:** 59 tests pass (12 identity, 19 Claim, 1 concurrency, 18 submit/track, 9 Queue/pick-up), with 0 build warnings. Build time at commit: 1h51m of the 5h cap.
- **My decision (12:39):** accepted D54–D59 as proposed, with no overrides. My reason: I agree with the AI's decisions; they make sense.

## Entry #15 (Thu 29 Oct 12:44): Codex joins every code review

- **Asked:** dispatch an agent to work out how to call Codex from the CLI with GPT 6.1 Sol, high reasoning and fast mode, and have it review #5 alongside the usual Claude review agents. Then I made it a rule: until all tickets are done, every review that uses Claude agents must also run Codex, and the results must be recorded.
- **AI did:**
  - A sub-agent confirmed each flag from the CLI's help, config and docs instead of guessing: `codex exec -m gpt-6.1-sol -c model_reasoning_effort="high" -c service_tier="fast" -s read-only`. The session log confirms the fast (`priority`) tier was used. The command and prompt checklist are in `docs/agents/codex-review.md`, and the rule is in AGENTS.md so it survives `/clear`.
  - It ran three reviews of commit `b23d6ed` in parallel: Codex, a Claude standards review and a Claude spec review.
- **Codex (GPT 6.1 Sol, high, fast; 71 seconds):** no findings on either axis. It reviewed statically and confirmed the `xmin` mapping against the Npgsql 10.0.3 docs. It didn't build or run tests, because the sandbox is read-only.
- **Claude spec review:** all 11 acceptance criteria met; 59/59 tests pass.
- **Claude standards review:** no hard breaches.
- **Fixed, from the Claude reviews:**
  - The "refused from every status" test now also checks that no Claim History entry, event or Assigned Officer was added. The spec asks each test to check those.
  - The unit tests no longer hard-code a seeded Officer's id; they don't touch the database, so any id works (D59).
  - The Queue breaks ties on Claim Number, so Claims created at the same instant can't swap places between polls.
  - A long chained call in the `xmin` test is split into two lines.
- **Left as is:**
  - The test `DemoUser` record repeats the seeded ids instead of reading `DemoUser.Seed`. The tests check against known literal values on purpose, so a wrong seed can't make them pass.
  - Copied submit setup across test files.
  - "Open" is defined only in the Queue query; Reporting builds Workload from events.
  - Any concurrency conflict gets the Claim's wording (D55).
  - The race test usually hits the status rule; the two-context test covers `xmin`.
  - Claimants now see the Assigned Officer id; the spec lists it on the Claim.
- **AI's view on Codex:** a clean result after a 71-second static review is weaker evidence than the Claude agents' review, which included a test run. It's still useful as an independent read that isn't anchored on Claude's.
- **Result:** 59 tests pass, with 0 build warnings. Build time at commit: 1h56m of the 5h cap.
- **Jay's correction (12:46):** run Codex as `codex --model gpt-6.1-sol -c model_reasoning_effort="high" -c service_tier="fast" "<prompt>"`. The AI pointed out that this form opens Codex's interactive UI, which its agents can't drive without a terminal. **I chose** to keep my flags but run them through `codex exec`, with a read-only sandbox and the review saved to a file. `docs/agents/codex-review.md` is updated.

## Entry #16 (Thu 29 Oct 12:58): ticket 04, Officer assesses and decides (#6, `/implement`)

- **Asked:** `/implement 6`: test-first, review (Claude agents plus Codex), commit and push. During the work I added a rule: log every bug found and how it was fixed, for every ticket from now on (now in AGENTS.md).
- **AI built, test-first at the agreed seams:**
  - **Seam 2 (the Claim, no database), 36 new tests:**
    - Set Reserve: sets it in the Market's currency; revises it; zero or less refused (400); refused from every status except Under Review (409); the timeline shows the amount unrounded.
    - Approve: moves to Approved at the Reserve; refused with no Reserve; refused from every status except Under Review.
    - Reject: moves to Rejected with the reason on the timeline; a blank reason refused; refused from every status except Under Review.
    - Settle: moves an Approved Claim to Settled; refused from every other status.
    - Another Officer is refused every action (403).
    - Every test also checks the Claim History entry and the event (ReserveSet with amount and currency; ClaimApproved, ClaimSettled with Claimant id; ClaimRejected with Claimant id and reason), and that a refused action changes nothing.
  - **Seam 1 (HTTP), 25 tests:** one happy path from submit to settle (each step on the timeline and in the outbox); reject with the reason seen by the Claimant; another Officer gets 403 on all four actions; a Claimant gets 403; approve without a Reserve is 409; a Settled Claim allows nothing further (409 naming each rule); settle before approval is 409; a bad Reserve or a blank reason is 400; an unknown Claim is 404.
  - **Follow-ups from #5, both done:** the Queue leaves out my Settled and Rejected Claims (HTTP test); the unit tests now reach each status through real transitions. Only Info Requested still uses the private setter, until #7 adds its method.
- **AI's implementation calls, for me to review** (D60–D65): who is checked first (403, then 409, then 400); the Reserve stored unrounded; the rejection reason kept in the timeline and event, not a column; one `Record` helper on the Claim and one `ActOnClaim` helper in the controller; decision events carry only the Claimant id; **the Claimant can see the Reserve** (flagged, because real insurers often keep reserves internal).
- **TDD note:** every Seam 2 slice was red first (a compile error or a failing assertion). The 25 HTTP tests passed on their first run, after the happy path, because the Claim's rules were already in place. To check they bite, the AI removed the 403 mapping as a test: 4 of them failed, then it was restored.
- **Bugs found and fixed:**
  - **AI test-setup bug:** the first Reserve test failed because the `InStatus` helper left the ClaimSubmitted event behind. Fixed by switching `InStatus` to real transitions that clear the events at the end (the #5 follow-up).
  - **Codex, hard issue:** the timeline rounded the Reserve to two decimals, so `0.001` showed as `SGD 0.00` while the database and the event kept `0.001`. A regression test went red first; the timeline now shows the amount unrounded, with at least two decimals (D61).
  - **Claude standards review, hard issue:** `NotAssignedOfficerException` cited D23, which doesn't cover the other-Officer 403. It now cites ADR-0006 and spec #2.
- **Code review: three reviews of the same diff, in parallel:**
  - **Codex (GPT 6.1 Sol, high, fast; 62 seconds, static only):** no spec findings; one hard issue (the rounding above, fixed).
  - **Claude spec review:** all acceptance criteria and both #5 follow-ups met; 119/119 tests pass. It noted two small additions (the 1000-character reason cap; the shared controller helper) and asked for D60+ to be logged; both are done.
  - **Claude standards review:** one hard issue (the D23 citation, fixed). **Also fixed:** the Reserve format now has a comment; `RejectClaimRequest` got its summary; a test name now covers the "not a number" case; the vague `Change` helper is now `ActOnClaim`; the five repeated "set time, add history, add event" lines are now one `Record` method.
  - **Left as is:**
    - Test DTOs and the submit helper are copied across three HTTP test files; each file names only the fields it checks.
    - The other-Officer unit test picks the action with a string switch; four separate tests would repeat the same setup.
    - `(amount, currency)` isn't its own Money type; the currency always comes from the Market (D7).
    - `claim.Property(c => c.ReserveAmount)` changes nothing; it is there to carry the "no precision" comment.
    - Decision events don't carry the Assigned Officer (D64).
- **Result:** 120 tests pass (12 identity, 55 Claim, 1 concurrency, 18 submit/track, 9 Queue/pick-up, 25 assess/decide), with 0 build warnings. Build time at commit: 2h10m of the 5h cap.
- **My decision (12:59):** accepted D60–D65 as proposed, including D65 (Claimants see the Reserve), with no overrides.

## Entry #17 (Thu 29 Oct 13:00): remaining tickets run in parallel waves

- **Asked:** I suggested running the remaining tickets in parallel, with several sub-agents using the same workflow, and asked the AI what it thought. I also asked that this decision and the parallel work be logged.
- **AI proposed:** yes, but in waves set by the blocking links, not all at once:
  - **Wave 1:** #7 Info Requests, #8 Kafka and emails, #10 Angular shell (nothing blocks them).
  - **Wave 2:** #9 read model (after #7 and #8); #11 Claimant screens and #12 Officer screens (after #7 and #10).
  - **Wave 3:** #13 Dashboard, then #14 README and fresh-clone check.
- **Risks it named, and how it handles each:**
  - **Shared test database:** each agent gets its own databases, named by ticket. `CLAIMS_TEST_DB` now picks the test database; the agents never touch `claims` or `claims_test`.
  - **Migration conflicts:** #7 and #8 both add EF migrations, which clash in the model snapshot. Each agent works in its own git worktree, and the AI merges them one at a time, regenerating a migration if needed.
  - **Journal and decision numbering:** agents don't edit AI_JOURNAL.md or `docs/decisions.md`. They report their bugs, review results and implementation calls. The AI writes the entries, with D-numbers and `date` times, as it merges each ticket.
  - **Same workflow per ticket:** test-first at the spec's seams, then two Claude review agents plus Codex (AGENTS.md), with every bug found and fixed logged.
  - **Cost and my review load:** more tokens per wave, and three tickets' code and decisions arrive at once for me to review and defend.
  - **`ng new` (#10):** needs my OK before any npm package is added.
- **My decision (12:59):** go ahead with wave 1 as proposed; `ng new` may install its default packages only.
- **Bug found while preparing, fixed:** with a brand-new test database, 4 of 120 tests failed ("relation \"outbox\" already exists"). xUnit runs test classes in parallel, each with its own `ApiFactory`, and each host applies migrations as it starts, so they raced to create the same tables. It never showed before because `claims_test` already existed, but it would have broken the fresh-clone check (#14) and every new worktree database. `ApiFactory` now starts hosts one at a time. That passed three runs on fresh databases, plus a run on the existing one.
- **Result:** 120 tests pass, with 0 build warnings. Build time at commit: 2h12m of the 5h cap.
- **AI slip, fixed:** it first wrote this entry as 13:03 without running `date`; `date` said 13:00, and the entry, time log and build time were corrected in the next commit.
- **My reasons (13:03):** (the AI first wrote 13:04 before running `date`; corrected)
  - **D60–D65:** I read the reasons in the decision log and agree with them; they make sense.
  - **Parallel waves:** to show the panel I can direct several agents safely. The waves follow the blocking links, and every ticket keeps the same reviews and logging.

## Entry #18 (Thu 29 Oct 13:02): wave 1 launched; Confluent.Kafka approved

- **Wave 1 launched (13:00–13:02):** three builder agents, each in its own git worktree with its own databases (`claims_t<N>`, `claims_test_t<N>`) and API port: #7 Info Requests, #10 Angular shell (in `web/`), and #8 Kafka and emails. Each works test-first at the spec's seams, commits to its own branch, doesn't push, and reports its bugs found and fixed and its implementation calls. The AI then runs two Claude review agents plus Codex on each branch, applies fixes, merges to main one ticket at a time, and writes the journal entry and D-numbers.
- **AI's call:** the Angular app goes in `web/` at the repo root, because no decision named a folder. It is logged as an implementation call for my review.
- **Package approval (13:01):** #8's acceptance criteria require me to confirm Confluent.Kafka. The AI's reason: it is Confluent's official .NET client for Kafka, it is the only package #8 needs, and the spec already lists it as approved. **I approved it**, saying the AI may add a package when there is a good reason, as long as it records the reason and my approval. The AI pinned it to an exact version (2.16.0, the latest stable). It reads my approval as covering this package only, and will still ask before any other.
- **`ng new` (#10):** approved at 12:59 (entry #17), with its default packages only.

## Entry #19 (Thu 29 Oct 13:08): decisions go to a council of Claude and Codex agents

- **Asked (13:06):** "for anything that needs a decision from me dispatch a council team of claude and codex agents. let them think, decide, discuss and come to a consensus then whatever that decision is log it down as that is the direction approved by me."
- **AI did:** wrote the procedure in `docs/agents/council.md`, and replaced the AGENTS.md rule "stop and ask before a package or a change to an agreed decision" with this one, so it survives `/clear`.
  - **Members:** two fresh Claude agents (one with a simplicity-and-defensibility lens, one with a correctness-and-spec lens) and Codex. None of them built the work.
  - **Rounds:** each decides alone; each then reads the other two positions and gives a final pick; two of three wins, and any dissent is recorded.
  - **Scope:** each ticket's implementation calls, new packages, changes to agreed decisions, and scope cuts. Actions outside the repo's normal work (repo settings, deletions, spending) and anything I ask to decide myself stay with me. The 3h and 4h warnings still come to me.
- **AI's note to me:** the panel looks at how I direct and challenge the AI. The journal will show that I delegated these calls to a council, and the AI will give me a one-line summary of each outcome so I can still defend it.
- **Also:** `.claude/worktrees/` (the parallel agents' worktrees) is now git-ignored.

