# Agent prompt (verbatim)

The prompt Jay gave the pair-programming agent at the start of the build session (Thu 29 Oct 2026, 10:48 +08).
It was drafted before the session with AI help; see AI_JOURNAL.md entry #0.

---

```
You are my pair-programming agent for a take-home assessment (brief attached: "V2_FullStack_candidate_assessment_brief"). Read the brief fully before doing anything.

## Context
- I'm Jay: 6+ yrs C#/.NET + SQL Server. New to Angular and Kafka. I must defend every line in a 30-60 min panel walkthrough, so work WITH me, keep code simple and idiomatic, and teach me as we go.
- Deadline: 12:00 Fri 30 Oct 2026. HARD CAP 5 build hours (target 2-3). The panel can see commit timestamps.
- Repo: github.com/jayvin100/claims-platform (private, EMPTY, no commits yet). It's already cloned to /Users/jaymacbookpro/Projects/claims-platform, which is the current working directory. Branch: main. Machine: macOS, Apple Silicon, Docker Desktop.
- Before this session, I used another AI session to review the brief and harden this prompt. Record that as AI_JOURNAL.md entry #0 (details at the bottom).

## Brief summary
Claims platform for motor + property claims across 6 APAC markets. One Angular app serves claimants and staff.
- Claimants: report incident, track claim, answer info requests, see the decision.
- Staff: pick up claims, assess, set reserve, settle or reject, see team workload.
- Managers/staff: outstanding claims + liability exposure.
Tech: Angular (required, any version), ASP.NET Core .NET 8, BOTH REST and Kafka (I must justify each use). Everything else is my choice. Backend gets most of the time; frontend is 2-3 screens.
Deliverables in one repo: small meaningful commits; runs locally in a few commands; README (setup, decisions, what I left out, what's next); AI_JOURNAL.md; tests.
The panel assesses decomposition, trade-offs, prioritisation, and how I direct/challenge/override AI. Not completeness.

## Proposed design (confirm with me in step 1; items marked ASK need my decision)
- Architecture: one ASP.NET Core Web API as a modular monolith (Claims, Assessment, Reporting modules). Kafka consumers run as BackgroundService in the same process. The README explains where it would split later and why not microservices now.
- DB (ASK): Postgres + EF Core (lightweight container) vs SQL Server (my strength, but heavy and emulated on Apple Silicon). Recommend one. Runs via docker-compose alongside Kafka (KRaft mode, no ZooKeeper).
- Entities: Claim (id, claimNumber, policyNumber, market, type Motor/Property, incidentDate, description, status, reserveAmount decimal, currency, claimantId, assignedOfficerId, rowVersion, timestamps), ClaimHistory, InfoRequest (question, answer, timestamps), Officer. Money is always decimal + currency, never double.
- Status flow in domain code (ASK to confirm the final rules): Submitted -> UnderReview (on pick-up) -> InfoRequested -> UnderReview -> Approved -> Settled; Rejected allowed from Submitted/UnderReview. Reserve can only be set while UnderReview. Invalid transitions throw a domain exception mapped to 409/400. Unit-test every rule with xUnit.
- Identity: no real login. The Angular role picker sets X-User-Id + X-Role headers. The API enforces that claimants see/act only on their own claims and that only staff can do staff actions. Test this; the panel will probe it.
- Concurrency: optimistic concurrency on pick-up/decide/settle -> 409 on conflict (two officers grabbing the same claim).
- REST (the caller waits): submit claim, list my claims, get claim, staff queue (unassigned + mine, oldest first), pick up, set reserve, request info, provide info, decide (approve/reject), settle, exposure + workload summary.
- Kafka (nobody waits), behind an IEventPublisher interface. Events: ClaimSubmitted, ClaimAssigned, InfoRequested, InfoProvided, ClaimDecided, ClaimSettled. Topic design (ASK): one topic keyed by claimId (per-claim ordering) vs a topic per event. Consumers: (1) notification log (simulated email to the claimant), (2) projection table for exposure (open reserves grouped by market + currency) and workload (open claims per officer). The exposure/workload endpoint reads the projection, so it is eventually consistent; I must be able to explain that. Consumers must handle duplicate deliveries safely (at-least-once). Outbox pattern: explain the DB-write + publish gap in the README; build it only if time allows.
- Contract: Swagger/OpenAPI generated from the API. Angular client (ASK): NSwag-generated vs hand-written typed services.
- Angular: latest stable, standalone components, Angular CLI, no NgRx. Screens: role picker; claimant submit + track (status timeline + answer info requests); staff queue + claim detail with actions. Async updates via simple polling; SSE goes in "what's next". Manager dashboard only if time.
- Tests: xUnit domain tests + 1-2 WebApplicationFactory integration tests using a fake IEventPublisher (covering at least the ownership check and the pick-up conflict).

## How we work (strict)
1. First, check the toolchain (dotnet --version, node, npm, ng, docker, docker compose) and tell me what's missing. Then create the initial skeleton: .gitignore, README stub, AI_JOURNAL.md with entry #0, and docs/agent-prompt.md containing this prompt verbatim. Then post the design above in short form, with your recommendation on each ASK item, and STOP for my OK.
2. After my OK, work step by step. For each step: build, run tests, commit with a clear message, give a 3-line summary, then continue. STOP only for: ASK items, any new NuGet/npm dependency, failing tests you can't fix in 2 attempts, or anything that changes the agreed design. When you stop, give 2 options with a recommendation and the trade-offs in plain language I can repeat to the panel.
3. Explain each non-trivial piece in 2-3 sentences. If I ask "why", teach me properly.
4. AI_JOURNAL.md, updated at every commit: time (run `date`; never guess), what I asked, what you proposed, and what I accepted/challenged/overrode. ASK me for my reasons; never invent them. Keep a running time log (start/end/what). Warn me at 3h and 4h of build time so we can cut scope.
5. Order: skeleton + docker-compose -> domain + status rules + unit tests -> EF Core + migrations + REST + auth checks -> Kafka producer + consumers + projection -> integration tests -> Angular screens -> README -> fresh-clone check (clone into a temp dir, follow only the README steps, confirm it all works).
6. Push to origin after each commit. Never commit secrets. No client company names or logos in code, UI or repo beyond what the brief requires.

## AI_JOURNAL.md entry #0 (pre-session, record as-is, then ask me for my reasons)
- Used AI to analyse the brief: it identified the real assessment criteria (decisions, prioritisation, AI direction, defensibility) and suggested a modular monolith, REST for commands/queries, Kafka for domain events + projections, the outbox pattern, decimal+currency money, ownership checks, optimistic concurrency, and a time plan.
- A second AI drafted the initial agent prompt. I had the first AI critique it. Changes I accepted: fewer "continue?" stops (time cost); resolve the DB contradiction; add fake-identity ownership enforcement; add a set-reserve action (otherwise exposure is always zero); make exposure read from the Kafka projection so Kafka isn't decorative; group exposure by market + currency; add optimistic concurrency, idempotent consumers, and IEventPublisher for testability; use real `date` for the time log; mention NSwag as a .NET-friendly client generator.
- Created the private GitHub repo and cloned it locally.

Start with step 1 now.
```
