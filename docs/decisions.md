# Decision log

Every design decision from the grilling session (Thu 29 Oct), with what it was chosen over and why. Decisions that are hard to reverse also have an ADR in [adr/](adr/). Domain terms are defined in [GLOSSARY.md](../GLOSSARY.md).

## How the decisions answer the brief

The brief lists questions to consider. This is where each one is answered.

| Brief question | Answer | Where |
|---|---|---|
| What are the core entities, and how do they relate? | Claim (owned by a Claimant, held by an Assigned Officer) with its Info Requests and Claim History. Policy is a typed-in number, not an entity. | D5, D8, D20, glossary |
| One service or more? What drives it? | One modular monolith; Reporting is the first to split, because it already only hears events. | D2, D21, ADR-0001 |
| What is synchronous, and what is event-driven? | REST for anything a user waits on; Kafka for events that nobody waits on. | D10, ADR-0003 |
| How does one Angular app serve two user types? | Role picker, identity headers, route guards per role; the API enforces the rules. | D9, D28, ADR-0006 |
| What is the frontend/backend contract, and who defines it? | The backend's OpenAPI document; the Angular services are typed against it. | D27 |
| How is the async nature of claims handled across both tiers? | Backend: outbox → Kafka → idempotent consumers. Frontend: polling. | D15, D16, D30 |
| What does an Officer need to manage their workload? | A Queue (unassigned plus their own claims, oldest first) and a Workload view across the Team. | D18, D17 |
| What does the system need to know about outstanding exposure? | Reserve per Claim; Exposure per Market in its own currency, from the event-fed read model. | D6, D7, D17 |

## Domain

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D1 | Two roles: Claimant and Officer. Any Officer sees Team Workload and Exposure. | A separate Manager role | A Manager role adds authorisation rules but no new behaviour. Listed under what's next. |
| D5 | Lifecycle: Submitted → Under Review ⇄ Info Requested; Under Review → Approved → Settled; Under Review → Rejected. | Rejecting straight from Submitted; withdrawal; reopening | Every decision then has an Assigned Officer behind it. Approved and Settled are separate because "liability accepted" and "paid" are different moments, and Exposure lasts until payment. |
| D6 | One amount, the Reserve, paid in full on settlement. | A separate negotiated settlement amount | A second amount adds a field and rules for little demo value. |
| D7 | Six fixed Markets, each with one currency; currency comes from the Market; Exposure grouped by Market. | Free currency entry; converting to one currency | Amounts in different currencies are never added together, so there's no FX logic to get wrong. |
| D8 | Policy Number is typed in and not verified. | A Policy entity with coverage checks | Policy administration is a separate system; it's an integration point under what's next. |
| D11 | Any Officer picks up a Submitted Claim and becomes its Assigned Officer; only they act on it after that. No reassignment. | Any Officer acting on any Claim; manager reassignment | One owner per Claim gives clear accountability and a one-line authorisation rule. |
| D12 | Rules live on the Claim: Reserve > 0 and only while Under Review; Approve needs a Reserve; one open Info Request at a time; answering returns the Claim to Under Review; incident date not in the future. State errors return 409, input errors 400. | Validation scattered across controllers | The rules are in one place and every one gets a unit test. |
| D19 | The Claimant enters Policy Number, Market, Claim Type, incident date and description. Claim Number is `CLM-<market>-<sequence>`. No attachments. | GUIDs shown to users; file uploads | A Claimant can quote a readable number on the phone. Uploads mean storage and scanning, so they're under what's next. |

## Architecture and data

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D2 | One ASP.NET Core API as a modular monolith. | Microservices | One deployable and one database: runs in a few commands and fits the time box. ADR-0001. |
| D21 | Modules: Claims (owns the Claim and all REST), Notifications and Reporting (Kafka consumers). | Claims / Assessment / Reporting | Claimant and Officer actions change the same Claim, so an Assessment module would split one entity's rules. ADR-0001. |
| D3 | .NET 10 (current LTS). | .NET 8 | The brief doesn't pin a version, .NET 10 is the installed SDK, and installing an older one gains nothing. |
| D4 | Postgres with EF Core. | SQL Server; SQLite | Native on Apple Silicon, small and fast to start. SQL Server runs emulated and slowly; SQLite doesn't behave like production for concurrency. ADR-0002. |
| D13 | Optimistic concurrency on the Claim using Postgres `xmin`; the losing Officer gets 409. | Pessimistic locks; an extra version column | No locks are held, the conflict is caught on save, and Postgres already keeps a row version. |
| D22 | One API project with a folder per module, plus one test project. Controllers, not minimal APIs. | A project per module; minimal APIs | Controllers are the familiar .NET idiom; two projects keep the tree and `dotnet test` simple. |

## Messaging

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D10 | REST for every command and query; Kafka for domain events consumed by a notification log and the Exposure/Workload read model. | REST only; commands over Kafka | "If someone is waiting, it's REST; if nobody is, it's Kafka." Kafka does real work, and the user never waits on the broker. ADR-0003. |
| D14 | One topic, `claim-events`, keyed by Claim id, event type in a header. | A topic per event type | Kafka orders messages only within a partition; keying by Claim id keeps each Claim's events in order. ADR-0005. |
| D15 | Transactional outbox: events are saved with the change and published by a background worker. | Publishing after `SaveChanges`; change data capture | A crash can delay an event but never lose it, and REST requests never touch the broker. CDC is another system to run. ADR-0004. |
| D16 | Every event has a unique id; consumers record processed ids in the same transaction as their update and skip repeats. | Assuming exactly-once delivery | The outbox makes delivery at-least-once, so duplicates will happen; this makes them harmless. |
| D17 | Read model: Exposure per Market (open Claims, Open Reserves, Unreserved count; changed after the Codex review, see D33); Workload per Officer (open Claims by status, Settled and Rejected counts); unassigned count. | Time-based measures such as average time to settle | Covers the brief's "workload and performance" and "exposure" from event data alone. More measures are under what's next. |
| D18 | The Queue reads live data over REST, oldest first. | Reading the Queue from the read model | Officers act on the Queue immediately; stale data would cause pointless 409s. |
| D32 | Eight events: ClaimSubmitted, ClaimPickedUp, ReserveSet, InfoRequested, InfoProvided, ClaimApproved, ClaimRejected, ClaimSettled. Each carries event id, Claim id, Market, time, and the fields consumers need (Assigned Officer, Reserve and currency, Claimant id). | The six events in the original prompt, with a combined ClaimDecided | Without ReserveSet the read model can't calculate Exposure. Separate Approved/Rejected events each mean one thing, so consumers don't branch on a field. |
| D20 | The Claimant's timeline comes from Claim History, saved with each change and read over REST. Notifications only go to a log table and the console. | Building the timeline from events; showing notifications in the UI | The timeline must be exact and immediate. The simulated email shows the "nobody waits" side of Kafka without another screen. |

## Identity and security

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D9 | No login: a role picker sends `X-User-Id` and `X-Role`, and the API trusts them. Authorisation checks are real and tested. | OIDC/JWT; no identity at all | Real login is plumbing that shows nothing about claims. The ownership checks are what matter, and they would read from a JWT unchanged. ADR-0006. |
| D23 | Another Claimant's Claim returns 404; a Claimant on an Officer endpoint gets 403. | 403 for both | 404 doesn't reveal whether a Claim Number exists; 403 is the honest answer for the wrong role. |
| D26 | Seeded demo users (2 Claimants, 3 Officers) behind `GET /api/users` for the role picker. | Hard-coding users in Angular | Three Officers make Workload and the pick-up conflict worth demoing. The endpoint is marked demo-only. |

## Testing and running

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D24 | xUnit tests for every Claim rule; `WebApplicationFactory` integration tests on a test database in the Compose Postgres (ownership, pick-up conflict, outbox row written); one consumer idempotency test; no Angular tests. | Testcontainers; a fake Kafka publisher; frontend tests | The conflict check needs real Postgres. The outbox means tests check a table instead of faking Kafka. The risk is in the backend rules. |
| D25 | Compose runs Postgres and Kafka only; the API (`dotnet run`) and Angular (`npm start`) run on the host. The API migrates, seeds and creates the topic on startup in Development. | Containerising everything | Four commands and no Dockerfiles to debug against the deadline. Full Compose is under what's next. |
| D31 | Dependencies, pinned: Npgsql EF Core provider 10.0.3, Confluent.Kafka 2.16.0, Angular CLI 22.2.5, a local `dotnet-ef` tool; the rest (EF Core Design, Microsoft.AspNetCore.OpenApi, Swashbuckle Swagger UI, xunit, test SDK, Mvc.Testing) at the latest compatible stable version, recorded in the journal when added. npm: only what `ng new` installs. | Hand-rolling any of these; floating versions | Each one replaces code we'd otherwise write. Pinning means a future break won't come from a dependency version changing. |

## Frontend

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D27 | Hand-written typed Angular services, plus one interceptor that adds the identity headers. | NSwag-generated client | About 12 endpoints; no generator or build step, and every line is explainable. The OpenAPI document is still the contract. |
| D28 | Standalone components, signals for state, a route guard per role, plain CSS. | NgRx; Angular Material | Signals are Angular's built-in state; NgRx is overkill for four screens. Material adds a dependency and a learning curve. |
| D29 | Screens in build order: role picker; Claimant list and submit; Claimant detail with timeline and Info Request answers; Officer Queue and detail with actions; dashboard (cut first). | Building the dashboard earlier | Screens 1–4 are the two user journeys. The dashboard is the visible proof of the Kafka read model, and Swagger can stand in for it. |
| D30 | Poll every 5 seconds on detail and list screens. | SSE; SignalR | A few lines with an RxJS timer and no server changes; claims move in hours, not seconds. SSE is under what's next. |

## After the Codex review (#15)

Codex reviewed the plan independently ([#15](https://github.com/jayvin100/claims-platform/issues/15)). Jay's decisions on its findings:

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D33 | Exposure reports Open Reserves plus a count of Unreserved Claims per Market. | One "Exposure" total of Reserves | Submitted Claims have no Reserve, so a single total would hide unpriced exposure as zero. |
| D34 | Keep building the outbox (D15) unconditionally. | Codex: build it only within a 15–20 minute gate, else publish after save | Jay's call; see AI_JOURNAL entry #11. |
| D35 | No scope cuts planned; Codex's 120-minute budget is the ceiling. | Codex's ordered cut list | Jay expects to finish inside the budget. The 3h and 4h warnings still apply. |
| D36 | Event and delivery fixes: Claimant id on ClaimPickedUp; outbox ordered by sequence number with one publisher that stops on failure; per-consumer processed-event records; offsets committed after processing; topic creation retried in the background; all Kafka off in tests; a two-context `xmin` test; Swagger and CORS preflight exempt from identity. | The original spec wording | Codex found real gaps with no trade-off to weigh. |
| D37 | Ticket links: #11 also waits on #6; the README ticket #14 no longer waits on the dashboard #13. | The original links | #11 renders Reserve and rejection, which #6 builds. Final verification shouldn't wait on a single screen. |
