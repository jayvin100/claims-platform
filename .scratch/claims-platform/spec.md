# Spec: Claims Platform

Status: ready-for-agent

Sources: the design grilling of Thu 29 Oct, [docs/decisions.md](../../docs/decisions.md) (D1–D32), [ADRs 0001–0006](../../docs/adr/) and [GLOSSARY.md](../../GLOSSARY.md). Terms in this spec are used as the glossary defines them.

## Problem Statement

Motor and property claims across six APAC markets are handled by phone, email, shared inboxes and spreadsheets.

- A **Claimant** reports an incident and then hears nothing. They can't see where their Claim is, they're asked for information by email, and they find out the decision late.
- An **Officer** has no single place to see which Claims are waiting, which are theirs, and what state each is in. Two Officers can end up working the same Claim.
- Whoever runs the **Team** has no current picture of how much work each Officer holds, or of the outstanding **Exposure** per Market.

## Solution

One Angular application, backed by one ASP.NET Core API, serves both Claimants and Officers.

- A Claimant submits a Claim online and gets a Claim Number straight away. They see its status timeline update as it moves, answer Info Requests in the app, and see the decision when it's made.
- An Officer works from a Queue of unassigned and their own Claims, oldest first. Picking a Claim up makes them its Assigned Officer; nobody else can then act on it. They set a Reserve, ask the Claimant questions, and approve, reject or settle.
- Any Officer can see the Team's Workload and the Exposure per Market. These views are fed by domain events over Kafka, so they lag the live data by a few seconds.
- Each step a Claimant cares about triggers a simulated email.

There is no real login: a role picker chooses a seeded demo user, and the API enforces who may see and do what.

## User Stories

### Choosing who you are (demo identity)

1. As a demo user, I want to pick a seeded Claimant or Officer from a list, so that I can use the app as that person without a login.
2. As a demo user, I want the app to remember my choice until I switch, so that I don't re-pick on every screen.
3. As a demo user, I want to switch user at any time, so that I can show the Claimant and Officer sides of the same Claim.
4. As a demo user, I want the app to show only the screens for my role, so that a Claimant never sees Officer actions.

### Claimant: reporting a Claim

5. As a Claimant, I want to report an incident by entering my Policy Number, Market, Claim Type, incident date and a description, so that I can start a Claim without phoning anyone.
6. As a Claimant, I want to be told straight away if a field is missing or invalid, so that I can fix it before submitting.
7. As a Claimant, I want to be stopped from entering an incident date in the future, so that my Claim isn't rejected for a typo.
8. As a Claimant, I want the currency to follow from the Market I choose, so that I never have to pick one.
9. As a Claimant, I want a Claim Number as soon as I submit, so that I have a reference to quote.
10. As a Claimant, I want my new Claim to show as Submitted, so that I know it was received.
11. As a Claimant, I want a confirmation email when I submit, so that I have a record outside the app.

### Claimant: tracking a Claim

12. As a Claimant, I want to see a list of all my Claims with their Claim Number, type, Market and status, so that I can see everything I have open in one place.
13. As a Claimant, I want my list to show newest first, so that the Claim I just reported is at the top.
14. As a Claimant, I want to open a Claim and see its details, so that I can check what I submitted.
15. As a Claimant, I want a timeline of every status change on my Claim, with dates, so that I can see how it has progressed.
16. As a Claimant, I want the Claim screens to refresh on their own, so that I see changes without reloading.
17. As a Claimant, I want to see only my own Claims, so that my information stays private and I can't see anyone else's.
18. As a Claimant, I want to know when an Officer has picked up my Claim, so that I know someone is working on it.

### Claimant: Info Requests

19. As a Claimant, I want to see an Officer's question on my Claim, so that I know what information is needed.
20. As a Claimant, I want to be emailed when an Officer asks me something, so that I don't have to keep checking.
21. As a Claimant, I want to answer the question in the app, so that I don't have to send an email.
22. As a Claimant, I want my answer to send the Claim back to the Officer, so that the review carries on without me chasing.
23. As a Claimant, I want to see past questions and my answers on the Claim, so that I have the full conversation in one place.

### Claimant: decisions

24. As a Claimant, I want to see when my Claim is Approved, so that I know it will be paid.
25. As a Claimant, I want to see when my Claim is Rejected, with the Officer's reason, so that I understand why.
26. As a Claimant, I want to see when my Claim is Settled, so that I know payment has been made.
27. As a Claimant, I want an email for each decision and for settlement, so that I hear about outcomes even if I'm not in the app.

### Officer: the Queue

28. As an Officer, I want a Queue of unassigned Submitted Claims plus my own open Claims, so that I see everything I can act on.
29. As an Officer, I want the Queue sorted oldest first, so that Claims aren't left waiting.
30. As an Officer, I want each Queue row to show Claim Number, Market, type, status, age and whether it's mine, so that I can choose what to work on.
31. As an Officer, I want the Queue to reflect the live state of each Claim, so that I don't try to pick up something already taken.
32. As an Officer, I want Claims held by other Officers left out of my Queue, so that I'm not distracted by work I can't do.

### Officer: picking up and assessing

33. As an Officer, I want to pick up a Submitted Claim, so that I become its Assigned Officer and it moves to Under Review.
34. As an Officer, I want a clear message if another Officer picked up the same Claim a moment before me, so that I know to move on rather than think the app is broken.
35. As an Officer, I want to open any Claim and see its details, timeline, Info Requests and Reserve, so that I have what I need to assess it.
36. As an Officer, I want to set a Reserve on a Claim Under Review, so that the expected cost is recorded.
37. As an Officer, I want to revise the Reserve while the Claim is still Under Review, so that it tracks what I learn.
38. As an Officer, I want a Reserve of zero or less refused, so that Exposure isn't distorted by mistakes.
39. As an Officer, I want the Reserve shown in the Claim's Market currency, so that there's no doubt what the amount means.

### Officer: Info Requests

40. As an Officer, I want to ask the Claimant a question on a Claim Under Review, so that I can get what I need to decide.
41. As an Officer, I want the Claim to move to Info Requested while I wait, so that my Queue shows it's waiting on the Claimant.
42. As an Officer, I want to be stopped from asking a second question while one is unanswered, so that the Claimant gets one clear request at a time.
43. As an Officer, I want to see the Claimant's answer on the Claim when it comes back Under Review, so that I can carry on assessing.

### Officer: decisions and settlement

44. As an Officer, I want to approve a Claim Under Review that has a Reserve, so that liability is accepted at that amount.
45. As an Officer, I want approval blocked until a Reserve is set, so that no Claim is approved without a known cost.
46. As an Officer, I want to reject a Claim Under Review with a reason, so that the Claimant knows why.
47. As an Officer, I want to settle an Approved Claim, so that it's recorded as paid and leaves the Exposure.
48. As an Officer, I want Settled and Rejected Claims to be final, so that closed decisions can't be changed by accident.
49. As an Officer, I want to be stopped from acting on a Claim assigned to someone else, so that each Claim has one accountable owner.
50. As an Officer, I want an action that doesn't fit the Claim's current status refused with a clear message, so that I understand why nothing happened.

### Team Workload and Exposure

51. As an Officer, I want to see each Officer's open Claims split by Under Review, Info Requested and Approved, so that I can see who is overloaded.
52. As an Officer, I want to see how many Claims each Officer has Settled and Rejected, so that there's a basic measure of output.
53. As an Officer, I want to see how many Submitted Claims nobody has picked up, so that I know the size of the waiting pile.
54. As an Officer, I want to see the Exposure per Market, with the number of open Claims and the total Reserve in that Market's currency, so that I know the outstanding liability.
55. As an Officer, I want Exposure never to add different currencies together, so that the totals mean something.
56. As an Officer, I want Exposure to drop when a Claim is Settled or Rejected, so that it reflects only what's still owed.
57. As an Officer, I want these views to catch up within seconds of a change, so that they're current enough to act on.

### Platform behaviour

58. As a Claimant, I want my action to succeed even if the messaging system is briefly down, so that reporting or answering never fails for reasons I can't see.
59. As the business, I want no domain event lost if the API crashes after saving a change, so that Exposure and notifications stay correct.
60. As the business, I want a duplicated event never counted twice, so that Exposure and Workload stay accurate.
61. As the business, I want all events for one Claim processed in the order they happened, so that a Claim is never counted as settled before it was picked up.
62. As a developer, I want the whole platform to start locally in a few commands, so that anyone can run and review it.
63. As a developer or reviewer, I want interactive API documentation, so that I can call every endpoint without the frontend.
64. As a reviewer, I want every authorisation rule enforced by the API rather than only hidden in the UI, so that calling the API directly can't get around it.

## Implementation Decisions

### Shape (ADR-0001, D2, D21, D22)

- One ASP.NET Core API on .NET 10, using controllers. It has three modules, each a folder:
  - **Claims** owns the Claim, its Info Requests and its Claim History, and exposes every REST endpoint that reads or changes them.
  - **Notifications** consumes events and writes simulated emails.
  - **Reporting** consumes events, keeps its own read model, and exposes the Workload and Exposure endpoints.
- Notifications and Reporting never read Claims tables. They only know what the events tell them. That is the seam a later split would follow.
- One test project holds both unit and integration tests.
- Shared infrastructure: the database context, the outbox worker, the Kafka producer, and the identity handling.

### The Claim and its rules (D5, D6, D11, D12, D13)

- The Claim holds:
  - id
  - Claim Number
  - Policy Number
  - Market, and the currency derived from it
  - Claim Type
  - incident date and description
  - status
  - Reserve (empty until set)
  - Claimant id
  - Assigned Officer id (empty until picked up)
  - created and updated times
- The Claim's public methods are the only way to change it: submit, pick up, set Reserve, request info, provide info, approve, reject (with a reason), and settle. Each method checks its rule and, on success, records a Claim History entry and raises one domain event.
- Allowed transitions:

  | From | Action | To | Who |
  |---|---|---|---|
  | (new) | submit | Submitted | Claimant |
  | Submitted | pick up | Under Review | any Officer (becomes Assigned Officer) |
  | Under Review | set Reserve | Under Review | Assigned Officer |
  | Under Review | request info | Info Requested | Assigned Officer |
  | Info Requested | provide info | Under Review | owning Claimant |
  | Under Review | approve (needs Reserve) | Approved | Assigned Officer |
  | Under Review | reject (needs reason) | Rejected | Assigned Officer |
  | Approved | settle | Settled | Assigned Officer |

- Every other combination is a domain rule violation. Further rules:
  - The Reserve must be greater than zero.
  - Only one Info Request can be open at a time.
  - The incident date can't be in the future.
  - Settled and Rejected are final.
- Errors map as follows:
  - A violated state rule returns **409** with a problem-details body that says which rule.
  - Invalid input returns **400**.
- Concurrency uses Postgres `xmin` as the Claim's concurrency token. If two requests change the same Claim at once, the second save fails and returns **409**. A pick-up that arrives after another has already been saved also returns 409, because the Claim is no longer Submitted.

### Markets (D7)

- The six Markets are fixed, each with one currency: SG (SGD), MY (MYR), HK (HKD), TH (THB), ID (IDR), PH (PHP).
- The currency is set from the Market and never accepted as input.
- Money is always a decimal amount with its currency code.

### Claim Number (D19)

- The format is `CLM-<market code>-<6-digit sequence>`, for example `CLM-SG-000123`.
- The sequence comes from a single database sequence shared across Markets.
- Routes use the Claim's internal id; the Claim Number is for display.

### Identity and authorisation (ADR-0006, D9, D23, D26)

- Every request except `GET /api/users` must carry `X-User-Id` and `X-Role` (`Claimant` or `Officer`). If either header is missing or doesn't match a seeded user, the API returns **401**.
- A Claimant asking for a Claim they don't own gets **404**.
- A Claimant calling an Officer-only endpoint gets **403**.
- An Officer acting on a Claim assigned to another Officer gets **403**.
- The checks read the current user from one place, so swapping the headers for JWT claims later changes only that place.
- Seeded demo users: 2 Claimants and 3 Officers, each with a fixed id, name and role.

### REST contract (D10, D18, D20)

The OpenAPI document generated from the API is the contract, with Swagger UI served in Development.

| Method and route | Who | Does |
|---|---|---|
| `GET /api/users` | anyone (demo only) | Lists the seeded users for the role picker |
| `POST /api/claims` | Claimant | Submits a Claim; returns 201 with the Claim, including its Claim Number |
| `GET /api/claims` | Claimant | Lists the caller's own Claims, newest first |
| `GET /api/claims/{id}` | owning Claimant, any Officer | Claim details, Claim History timeline and Info Requests |
| `POST /api/claims/{id}/info-response` | owning Claimant | Answers the open Info Request |
| `GET /api/queue` | Officer | Unassigned Submitted Claims plus the caller's open Claims, oldest first (live data) |
| `POST /api/claims/{id}/pick-up` | Officer | Assigns the caller and moves the Claim to Under Review |
| `PUT /api/claims/{id}/reserve` | Assigned Officer | Sets or revises the Reserve (amount only) |
| `POST /api/claims/{id}/info-requests` | Assigned Officer | Asks the Claimant a question |
| `POST /api/claims/{id}/approve` | Assigned Officer | Approves at the current Reserve |
| `POST /api/claims/{id}/reject` | Assigned Officer | Rejects, with a reason |
| `POST /api/claims/{id}/settle` | Assigned Officer | Marks an Approved Claim as Settled |
| `GET /api/reports/workload` | Officer | Workload per Officer plus the unassigned count, from the read model |
| `GET /api/reports/exposure` | Officer | Exposure per Market, from the read model |

Responses that change a Claim return its updated state, so the frontend doesn't need a second call.

### Domain events and Kafka (ADR-0003, ADR-0005, D14, D32)

- There are eight events: ClaimSubmitted, ClaimPickedUp, ReserveSet, InfoRequested, InfoProvided, ClaimApproved, ClaimRejected, ClaimSettled.
- Every event carries an event id (unique), the Claim id, the Claim Number, the Market and the time it happened. Each event also carries the fields its consumers need:

  | Event | Extra fields |
  |---|---|
  | ClaimSubmitted | Claimant id, currency |
  | ClaimPickedUp | Assigned Officer id |
  | ReserveSet | amount, currency |
  | InfoRequested | Claimant id, question |
  | ClaimRejected | Claimant id, reason |
  | ClaimApproved, ClaimSettled | Claimant id |

- All events go to the single topic `claim-events`. The message key is the Claim id, the value is the event as JSON, and the event type travels in a message header.
- Notifications and Reporting each use their own consumer group, so each receives every event. A consumer commits its offset only after it has handled the message.

### Outbox (ADR-0004, D15, D16)

- When a Claim changes, its event is written to an outbox table in the same database transaction. The outbox row holds the event id, type, Claim id, JSON payload, time it happened, and time sent (empty until published).
- A background worker polls every second. It publishes unsent rows in the order they happened, and marks each one sent after Kafka acknowledges it. REST requests never talk to Kafka.
- This gives at-least-once delivery. Each consumer records the ids of the events it has processed, in the same transaction as its own changes, and skips any id it has already recorded.

### Reporting read model (ADR-0003, D17)

- Reporting keeps its own table with one row per Claim, holding:
  - Claim id
  - Market and currency
  - status
  - Reserve
  - Assigned Officer
- Each event inserts or updates that row.
- The Workload and Exposure endpoints compute their totals from this table:
  - **Exposure:** per Market, the open Claim count and the total Reserve, for Claims that are neither Settled nor Rejected.
  - **Workload:** per Officer, open Claims split by Under Review, Info Requested and Approved, plus Settled and Rejected counts.
  - **Unassigned:** the count of Submitted Claims.
- Keeping current state per Claim, rather than running totals, means no add/subtract arithmetic can drift. Recording processed event ids still stops duplicates from re-applying old changes.

### Notifications (D20)

- For ClaimSubmitted, ClaimPickedUp, InfoRequested, ClaimApproved, ClaimRejected and ClaimSettled, the consumer writes a simulated email to the Claimant into a notification log table and to the console.
- It ignores ReserveSet and InfoProvided, which aren't news to the Claimant.
- Notifications are not shown in the UI.

### Running locally (D25, D31)

- Docker Compose runs Postgres and Kafka (KRaft mode, `apache/kafka` image). The API and Angular run on the host.
- In Development, the API does three things on startup: applies migrations, seeds the demo users, and creates `claim-events` if it doesn't exist.
- Approved packages:
  - NuGet: Npgsql EF Core provider, EF Core Design, Confluent.Kafka, Microsoft.AspNetCore.OpenApi, Swashbuckle Swagger UI, xunit, Mvc.Testing.
  - npm: only what `ng new` installs.
  - Agents still confirm with Jay before installing each package.

### Frontend (D27–D30)

- An Angular app (latest stable) using standalone components, signals for state and plain CSS. No NgRx or component library.
- Typed services are written by hand against the REST contract. One HTTP interceptor adds the identity headers from the chosen demo user.
- A route guard per role.
- Screens, in build order:
  1. Role picker.
  2. Claimant: My Claims list plus the submit form.
  3. Claimant: Claim detail with timeline, Info Requests and the answer box.
  4. Officer: Queue plus Claim detail with actions (pick up, set Reserve, request info, approve, reject with reason, settle). Each action is shown only when the Claim's status allows it.
  5. Dashboard with the Workload and Exposure tables. It is the first screen to cut if time runs short.
- List and detail screens poll every 5 seconds.
- Error responses are shown in plain words, using the problem-details message.

## Testing Decisions

- **What makes a good test:**
  - It exercises behaviour through a seam's public interface and asserts on outcomes a caller can see: status codes, response bodies, resulting state, rows a consumer would read.
  - It never asserts on private methods, EF internals or call sequences.
  - Each test name states the rule it protects, in glossary terms.
- **Seam 1: HTTP API.**
  - `WebApplicationFactory` against a dedicated test database in the Compose Postgres, with the outbox worker turned off.
  - Covers:
    - a Claimant reading another Claimant's Claim (404)
    - a Claimant calling an Officer endpoint (403)
    - an Officer acting on another Officer's Claim (403)
    - missing identity headers (401)
    - two Officers picking up the same Claim (one succeeds, the other gets 409)
    - each change writing its event to the outbox (row present with the right type and Claim id)
    - one happy path from submit to settle
  - Real Postgres is required because concurrency relies on `xmin`.
- **Seam 2: the Claim's public methods.**
  - Plain xUnit with no database.
  - One test per allowed transition, and one per blocked transition or rule:
    - Reserve must be greater than zero, and can be set only while Under Review
    - approve needs a Reserve
    - only one open Info Request
    - no future incident date
    - Settled and Rejected are final
    - reject needs a reason
  - Each test also checks that the right Claim History entry and domain event were raised.
- **Seam 3: Reporting's apply-event method.**
  - xUnit against the test database, with no Kafka.
  - Covers:
    - the same event applied twice counts once
    - Exposure per Market excludes Settled and Rejected Claims and never mixes currencies
    - Workload counts move correctly through a Claim's lifecycle
- **Not tested automatically:**
  - the outbox worker's publishing
  - the Kafka producer and consumer adapters
  - the Notifications consumer
  - the Angular app

  These are checked by hand in the fresh-clone run.
- **Prior art:** none yet; the repo has no code. The first tickets set the pattern for each seam.

## Out of Scope

- Real authentication (OIDC/JWT), a Manager role, and reassigning Claims between Officers.
- Policy verification, coverage checks and policyholder lookup.
- Attachments and file uploads.
- Claimant withdrawal, reopening closed Claims, and timeouts on unanswered Info Requests.
- Partial or negotiated settlements, payments, and currency conversion.
- Real email delivery, and showing notifications in the UI.
- Time-based performance measures (for example, average time to settle).
- Server push (SSE/SignalR); the UI polls.
- Containerising the API and frontend, deployment, and CI.
- Angular tests, and automated tests of the Kafka adapters.
- A separate project per module; splitting any module into its own service.

## Further Notes

- **Gaps filled while writing this spec**, not decided in the grilling. Jay confirmed all five:
  - Reject requires a reason, and the Claimant sees it.
  - Missing or unknown identity headers return 401.
  - An Officer acting on another Officer's Claim gets 403 rather than 404, because Officers may see every Claim.
  - Reporting's read model keeps current state per Claim, not running totals.
  - Notifications skip ReserveSet and InfoProvided.
- **Eventual consistency, to explain in the demo:** after an Officer settles a Claim, the Queue and the Claim detail change immediately, because they're read live. The Exposure view changes a second or two later, once the outbox worker has published the event and Reporting has consumed it.
- **Where it would split first:** Reporting. It has its own tables and only hears events, so it could move to its own service and database without changing Claims.
- **Build time so far:** started 10:48 Thu 29 Oct. Spec written at 11:08. Hard cap 5 hours; warn at 3h and 4h.
