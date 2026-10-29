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

## Ticket 01: runnable skeleton (#3)

Made while building the skeleton. Jay chose D38–D40 when asked. D41–D46 are the AI's implementation calls, made without asking because they don't change an agreed decision; they are listed for Jay to review and override (AI_JOURNAL entry #12).

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D38 | Package versions added: Microsoft.EntityFrameworkCore.Design 10.0.14, Microsoft.AspNetCore.OpenApi 10.0.14, Swashbuckle.AspNetCore.SwaggerUI 10.3.1, Microsoft.AspNetCore.Mvc.Testing 10.0.14, Microsoft.NET.Test.Sdk 18.11.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.5, local tool dotnet-ef 10.0.14 (plus the D31 pin Npgsql EF 10.0.3). coverlet.collector, which the xunit template adds, was removed. | Template defaults; keeping coverlet | Latest stable at the time. Coverage isn't a deliverable and coverlet wasn't approved. |
| D39 | xunit 2.9.3. | xunit.v3 4.0.2 | It is what `dotnet new xunit` on .NET 10 still generates and what most `WebApplicationFactory` examples use, so there's less to explain. |
| D40 | Pin Microsoft.EntityFrameworkCore.Relational 10.0.14 in the API. | Lowering EF Core Design to 10.0.4; leaving the warning | Design is build-only and doesn't flow to tests, so tests got EF 10.0.4 from Npgsql while the API ran 10.0.14 (warning MSB3277). The pin gives both the latest EF patch. |
| D41 | Compose Postgres listens on host port 5433. | 5432 | A locally installed Postgres on 5432 (found on Jay's machine) silently takes the connection. 5433 avoids that for anyone running the repo. |
| D42 | Images `postgres:18.6-alpine` and `apache/kafka:4.3.2`, both multi-arch with native arm64. Kafka auto-create of topics is off. | `apache/kafka:4.4.0` (released days ago); topic auto-create | A version with some time in the wild. The API creates `claim-events` itself (later ticket), so a typo can't create a stray topic. |
| D43 | Demo identity is an ASP.NET Core authentication scheme (`DemoHeaderAuthenticationHandler`) plus a fallback policy requiring a signed-in user; `[AllowAnonymous]` opts `GET /api/users` out. `CurrentUser` reads the signed-in user, never the headers. | Custom middleware that writes 401s; reading headers in each controller | It is the framework's own extension point, so the JWT swap (ADR-0006) is one registration in `Program.cs`, and later role checks can use `[Authorize(Roles = "Officer")]`. A request to an unknown route without headers also gets 401. |
| D44 | Demo users are seeded with EF `HasData`, so the rows are part of the migration. | A seeding class run at startup | Migrations only apply once, so starting the API twice can't duplicate users, with no seeding code to test. |
| D45 | The 401 tests call a test-only endpoint (`/test/whoami`) registered from the test project. | Adding a real endpoint (e.g. `GET /api/me`) not in the REST contract | Keeps the contract unchanged; later tickets can test against real Claims endpoints. |
| D46 | The OpenAPI document declares the two headers, so Swagger UI has an "Authorize" button; `[AllowAnonymous]` endpoints show no lock. CORS allows `http://localhost:4200` with the two headers. | Reviewers adding headers by hand in another tool | Spec story 63 (call every endpoint without the frontend); CORS is needed for the Angular app and the preflight exemption. |

## Ticket 02: Claimant submits and tracks a Claim (#4)

D47–D53 are the AI's implementation calls, made without asking because they don't change an agreed decision; Jay reviewed them and accepted all seven (AI_JOURNAL entry #13).

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D47 | "Not in the future" is judged by the date on the Market's own clock (its capital's time zone: Asia/Singapore, Asia/Kuala_Lumpur, Asia/Hong_Kong, Asia/Bangkok, Asia/Jakarta, Asia/Manila). | The UTC date | All six Markets are 7–8 hours ahead of UTC, so a Claimant reporting a same-day incident before 07:00/08:00 local would be refused. Known gap: eastern Indonesia is 2 hours ahead of Jakarta. |
| D48 | Input limits: Policy Number up to 50 characters, description up to 2000. Market and Claim Type must be sent as names (`"SG"`, `"Motor"`); a number in their place is a 400. | No limits; accepting enum numbers | Bounded columns, and `0` can't silently mean Singapore. |
| D49 | The Claim Number sequence value is taken before the Claim checks its rules, so a refused submit leaves a gap. | Checking the rules before taking a number | D19 promises unique numbers, not consecutive ones; checking first would move a rule out of the Claim. |
| D50 | `AppDbContext.SaveChanges` turns every Claim's raised events into outbox rows in the same save, and clears them only after the save succeeds. | Each controller action writing its own outbox rows | One place, so no later action can forget its event, and one `SaveChanges` is one transaction. |
| D51 | Claim rule errors are exceptions, turned into problem details by one `IExceptionHandler`: `InvalidClaimException` → 400. State rules (409) get their own exception in #5. | Catching errors in each controller action | Controllers stay free of try/catch, and the Claim's own message reaches the user unchanged. |
| D52 | A Claim History entry holds the status after the action, a description and the time, ordered by its database id. The Claim gains Reserve and Assigned Officer in the tickets that use them, each with its own migration. | Adding every spec field now | No columns without behaviour or tests behind them. |
| D53 | `GET /api/claims` returns a summary row (Claim Number, Market, Claim Type, status, created time); `POST` and `GET /api/claims/{id}` return the full Claim with its Claim History. | One shape for both | The list doesn't load every Claim's history. |

## Ticket 03: Officer Queue and pick-up (#5)

D54–D59 are the AI's implementation calls, made without asking because they don't change an agreed decision; Jay reviewed them and accepted all six (AI_JOURNAL entry #14).

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D54 | `xmin` is mapped as a shadow `uint` property (`Version`) with `IsRowVersion()`. The migration lists an `xmin` column, but Npgsql generates no SQL for it, because Postgres already has it on every row. | A `Version` property on the Claim; an extra version column | The Claim's rules never read the version, so it stays out of the domain class. The two-context test proves EF adds `WHERE xmin = ...` to the update. |
| D55 | State rules throw `ClaimConflictException` → 409 with the Claim's message. Any `DbUpdateConcurrencyException` → 409 "Someone else changed this Claim a moment ago. Reload it and try again." Both are handled by the one `ClaimExceptionHandler`. | Catching the EF exception in each controller action | One place, as D51. The wording is Claim-specific; that holds while only the Claim has a concurrency token. |
| D56 | The Queue row is Claim Number, Market, Claim Type, status, created time and `IsMine`; the UI works out the age from the created time. | Returning the Assigned Officer id; an age field | Spec story 30 asks for "whether it's mine". An age computed on the server goes stale while the page polls. |
| D57 | The Queue filter is "status is Submitted, or held by me and neither Settled nor Rejected", with an index on Assigned Officer id. | Also checking "no Assigned Officer" | Pick-up sets the status and the Assigned Officer together and there's no reassignment (D11), so a Submitted Claim never has an Assigned Officer. |
| D58 | The unit test for "refused from every other status" sets the status through the Claim's private setter, the way EF Core does when it loads a row. "Already picked up" is also tested through the real `PickUp`. **Resolved by #6 and #7:** #6 reached every status but Info Requested through real transitions, and #7 replaced that last one with `RequestInfo`. The tests' `InStatus` helper now reaches every status through the Claim's own methods; no test sets the status by reflection. | Waiting for #6/#7 to add the methods that reach those statuses | The ticket asks for every status now. Each later ticket can switch its status to a real transition. |
| D59 | Picking up an unknown Claim id returns 404. The seeded identities used by tests live in one `TestUsers` helper. | 409; copying the identities into each test file | Nothing to pick up is "not found", not a conflict. A third test file would otherwise copy the same ids again. |

## Ticket 04: Officer assesses and decides (#6)

D60–D65 are the AI's implementation calls, made without asking because they don't change an agreed decision; they await Jay's review (AI_JOURNAL entry #16).

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D60 | Each Officer action checks the Assigned Officer first (403, `NotAssignedOfficerException`), then the status rule (409), then the input (400); a malformed body is 400 first (amended by D66). A Submitted Claim has no Assigned Officer, so acting on it before pick-up gets the status rule's 409. | Status rule first; 403 on a Submitted Claim too | Another Officer always gets the same 403 on a held Claim, whatever its status. On an unassigned Claim nobody holds it, so the useful message is the status rule. |
| D61 | The Reserve is stored as Postgres `numeric` with no fixed scale, and the timeline shows it unrounded, with thousands separators and at least two decimals (`SGD 1,000.005`). | `numeric(18,2)`; refusing more than two decimals | The saved amount, the ReserveSet event and the timeline always agree; nothing is rounded silently. Refusing extra decimals would be a new rule for Jay to decide (Codex review finding). |
| D62 | The rejection reason is kept in the Claim History entry ("Claim rejected: <reason>") and the ClaimRejected event, not in a column on the Claim. It is required and at most 1000 characters. | A `RejectionReason` column | The spec's Claim field list has no reason, and the timeline is where the Claimant reads it. |
| D63 | One private `Record` on the Claim sets the updated time, adds the Claim History entry and raises the event for every action. One `ActOnClaim` helper in the controller loads the Claim (404 if unknown), applies the action, saves and returns it. | Repeating those lines in each action | No action can forget its history entry or event, and the controller actions are one line each. |
| D64 | ClaimApproved, ClaimRejected and ClaimSettled carry only the Claimant id, as the spec's event table says, not the Assigned Officer. | Adding the Assigned Officer id to every event | Reporting keeps one row per Claim (D17) and learns the Officer from ClaimPickedUp; there is no reassignment (D11), so it never changes. |
| D65 | The Claimant sees the Reserve: it is on the Claim response for both roles, and each ReserveSet is on the timeline. | Hiding the Reserve from Claimants | The ticket says each action appears on the Claimant's timeline, and D53 gives both roles one Claim shape. Real insurers often keep reserves internal, so this is flagged for Jay. |

## Wave 1 council: #7, #8, #10 (council, approved under Jay's delegation)

A council of two Claude agents (simplicity lens, correctness lens) and Codex decided wave 1's implementation calls on Thu 29 Oct 13:20–13:23, under Jay's standing delegation (docs/agents/council.md). Each row is the direction Jay approved. Votes and dissent are in AI_JOURNAL entry #23.

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D66 | D60 amended: a malformed request body (missing or blank required field, too long) gets the framework's model-validation 400 first; D60's 403, then 409, then 400 order applies to well-formed requests. Tests pin it (another Claimant's blank answer, another Officer's blank reason: 400). | Removing the attributes and validating in the domain after the 403, 404 and 409 checks | The spec sets no order. The 400 comes before the Claim is loaded, so it is the same for any id and reveals nothing (D23 holds). Option (b) would rework merged #6 code for no spec gain. Unanimous after round 2; Codex first picked (b). |
| D67 | "Only the owning Claimant can answer" stays in the controller (`FindVisibleClaim`, 404), not on the Claim. Officer assignment stays on the Claim (403). | An owner check on the Claim | Visibility is a query concern; D23 hides another Claimant's Claim as 404, and a check on the Claim would be a second check that can never fire. Unanimous. |
| D68 | The notifications consumer lists the events with no email (ReserveSet, InfoProvided) and records them as processed. An unknown event type logs a warning and is recorded as processed. A unit test lists all eight event types (D32) and asserts each is emailed or ignored. | (a) `_ => null`, skipping anything silently; (b) throwing on an unknown type so it retries | Retrying an unknown type blocks every Claim on that partition until a deploy; the coverage test catches a missing mapping at build time. 2 of 3. **Dissent (Codex):** retry unknown types, because the test cannot protect against events added outside this code. |
| D69 | Events are published in order per Claim, not globally. The publisher sends unsent rows by sequence; a lower sequence that commits late is still sent, just after a higher one. | Coordination for strict global order | Consumers key on the Claim id and need each Claim's events in order; `xmin` (D54) and one event per save keep that. Unanimous. |
| D70 | CORS for `localhost:4200` stays, in Development only (D46). | The Angular dev-server proxy | Built, reviewed and agreed already; a proxy changes an agreed decision for no gain. Unanimous. |
| D71 | The identity interceptor adds the Demo User headers only when the request's origin equals the API's origin. | `req.url.startsWith(apiBaseUrl)` | A prefix match also sends the headers to `http://localhost:51690`. Codex objected in round 1; unanimous in round 2. |
| D72 | Accepted as built. #10: the `requireRole` guard factory and `noUserGuard`; Switch user clears the stored user; inline templates, plain CSS, standalone, signals, zoneless, the app in `web/`, no frontend test runner. #7: the one-open-request 409 before the status 409; history says "Info requested"/"Info provided" without the text (it is on the Info Request); question at most 1000 characters, answer at most 2000; `IsOpen` means no answer yet; `AsSingleQuery` when loading a Claim with both lists. #8: a shared consumer base class; on failure, seek back and retry after a delay; 3 partitions, topic created at startup in Development only; 10 s send timeout; no-email events recorded as processed; earliest offset; the email "recipient" is the Claimant id (console line plus `notification_log` row); topic metadata refreshed every 5 s. | | No member objected. |

## Wave 2 council: #9, #11, #12 (council, approved under Jay's delegation)

A council of two Claude agents (simplicity lens, correctness lens) and Codex decided wave 2's implementation calls on Thu 29 Oct 13:38–13:39, under Jay's standing delegation (docs/agents/council.md). Each row is the direction Jay approved. Votes and dissent are in AI_JOURNAL entry #30.

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D73 | Amounts in the Angular app are JS numbers (doubles). The API, the database and the Claim History stay exact (D61); the UI is exact to about 15 significant digits, both when a Reserve is typed and sent and when it is shown. Documented in a comment in `web/src/app/claims.ts` and in the README. | (b) the API sends the Reserve as a string; (c) a new rule capping the Reserve's digits | Real Reserves sit far inside 15 digits. (b) changes the REST contract and (c) amends D61, both to fix a display edge. Unanimous. |
| D74 | Reporting throws on an unknown event type: the message is retried, not recorded as processed, and its partition waits for a fix. This is a scoped exception to D68, which still applies to Notifications. The eight types are listed once. | Skipping and recording unknown types, as Notifications does | Skipping in Notifications loses one email; skipping in Reporting leaves the totals wrong for good, because a replay skips the recorded id. A stalled, then correct, dashboard is better than a silently wrong one. Unanimous; it also settles Codex's D68 dissent for Reporting. |
| D75 | The Dashboard lists every Officer from `GET /api/users`, with zero counts for Officers Reporting hasn't heard of. Reporting's API stays as built. | (a) documenting that an Officer with no Claims is missing; (c) Reporting reading the seeded users | Story 51 says "each Officer", and Reporting still reads only events. Unanimous. |
| D76 | Reporting takes the currency from `ClaimSubmitted` and `ReserveSet`. `Markets.CurrencyOf` is kept only as the fallback, for a row first created by another event and for Exposure's zero rows. Reporting keeps sharing the Claims enums. | (b) Reporting's own enums; (c) leaving the currency to `Markets.CurrencyOf` | The spec says Reporting "only knows what the events tell them". 2 of 3, after both Claude agents swapped sides in round 2. **Dissent (Claude A):** two sources for one fact are harder to defend than the one fixed D7 rule. |
| D77 | The glossary's Workload is the open Claims an Officer holds, split by status; the Settled and Rejected counts are shown beside it as output. | Renaming the Settled and Rejected counts | It matches D17 and stories 51–52 without changing the API. Unanimous. |
| D78 | Accepted as built. #9: `ProcessAsync` public so tests run without Kafka; any event can start a Claim's row; no "final" guard in Reporting (processed ids and D69 cover repeats); Exposure always lists six Markets; Workload returns Officer ids, named by the Dashboard; Reporting tests run serially from an empty table. #12: Approve disabled until a Reserve is set; detail at `/queue/:id`; Assigned Officer shown as "You", "Another Officer" or "Nobody yet"; single-field forms read the input element; no "open any Claim" search. #11: routes `/my-claims/new` and `/my-claims/:id`; the client checks the incident date on the Market's clock (D47); the rejection reason is read from the "Claim rejected: " history text (D62); one "This Claim was not found." for every 404; reactive forms for the multi-field submit form. | | No member objected. Two notes: the app has two form styles (#12's single fields, #11's validated form), and the "Claim rejected: " prefix couples the UI to the API's wording. |

## Final council: whole-repo review and #13 (council, approved under Jay's delegation)

A council of two Claude agents (simplicity lens, correctness lens) and Codex decided the open findings of the final whole-repo review, and #13's implementation calls, on Thu 29 Oct 13:52–13:55, under Jay's standing delegation (docs/agents/council.md). Each row is the direction Jay approved. Votes are in AI_JOURNAL entry #34.

| # | Decision | Chosen over | Why |
|---|---|---|---|
| D79 | A Kafka message a consumer can't read (no event-type header, empty value, bad JSON, no event id) is logged and committed past by Notifications; Reporting keeps retrying it (D74). Database and other errors still retry everywhere. | (b) both skip; (c) leave it and list it as a known limit | It is the same case D68 and D74 already settle for unknown types. 2 of 3 (Claude B, Codex); Claude A picked (c) but said it could accept (a). |
| D80 | Optimistic concurrency (`xmin`) catches concurrent saves, not a stale screen: an approve from an old tab approves at the current Reserve, as the spec's "Approves at the current Reserve" says. A README known limit; ETag/If-Match is the next step. | (b) Approve sends the Reserve it showed; (c) ETag/If-Match now | Either fix changes the REST contract late in the build. Unanimous. |
| D81 | Exposure keeps grouping by Market and currency. Events that disagree on a Market's currency would show as a separate, visible row, never as a mixed sum; a comment says so. | (b) Reporting refuses a mismatched currency; (c) grouping by Market only | (c) could add currencies together (D7); (b) adds a new way to block a partition and reopens D76. Unanimous. |
| D82 | Extreme amounts (around 1e28) can overflow the Exposure sum; a README known limit. | (b) a cap on the Reserve | A cap amends D61 and D73 for absurd input, and a per-Reserve cap still can't bound a sum over many Claims. Unanimous. |
| D83 | The outbox table only grows and its poll has no partial index; a README known limit (production adds the index and an archive job). | (a) adding the index now | The index alone doesn't solve growth, and a late migration would need another fresh-clone check. Unanimous. |
| D84 | ADR-0005 reworded: several publishers would send duplicates, which consumers skip by event id; one publisher is simpler and is the supported deployment. | Keeping "several publishers would break ordering" | Each publisher sends in sequence order and stops on failure, and `xmin` stops one Claim's changes committing out of order, so the first copy of each event still arrives in order. Unanimous. |
| D85 | Accepted as built (#13): header links to the Queue and Dashboard for Officers only; both reports fetched together each poll; users fetched in the poll until loaded once, then reused, with nothing shown until then (D75); a failed poll keeps the last figures and shows a message; no grand total, because it would add currencies. | | No member objected. |

