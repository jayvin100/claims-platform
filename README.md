# Claims Platform

A claims platform for motor and property claims across six APAC Markets. Claimants report Claims and track them. Officers pick them up, assess them, and approve, reject or settle them. One Angular app serves both. An ASP.NET Core API is the backend. It uses REST for anything a user waits on and Kafka for domain events.

Domain terms (Claim, Officer, Reserve, Exposure and so on) are used as [GLOSSARY.md](GLOSSARY.md) defines them.

## Prerequisites

- .NET 10 SDK
- Node.js 22.22+, 24.15+ or 26+ (what Angular 22 supports), with npm
- Docker, with Docker Compose

## Run it

Run these from the repo root, each long-running one in its own terminal.

```sh
docker compose up -d --wait          # Postgres (host port 5433) and Kafka (host port 9092)
dotnet tool restore                  # the local dotnet-ef tool
dotnet run --project src/Claims.Api  # the API on http://localhost:5169
```

On startup in Development, the API applies migrations, seeds five demo users and creates the Kafka topic `claim-events`. Kafka is on by default (`Kafka:Enabled` in `src/Claims.Api/appsettings.json`). If Kafka isn't up yet, the API still serves REST and keeps retrying in the background. On the very first run, EF Core may log one `fail: ... An error occurred using the connection to database 'claims'` line before it creates the database. It is harmless.

In a second terminal:

```sh
cd web
npm install
npm start                            # the Angular app on http://localhost:4200
```

Recent npm versions may warn that some packages' install scripts were not run (`install-scripts ... not yet covered by allowScripts`). The app builds and runs without them, so you can ignore the warning.

| What | Where |
|------|-------|
| The app | http://localhost:4200 |
| The API | http://localhost:5169 |
| Swagger UI | http://localhost:5169/swagger |

In Swagger, call `GET /api/users` first. Then use **Authorize** to enter a user's `id` as `X-User-Id` and their `role` as `X-Role`. Every other endpoint needs both headers.

## Run the tests

```sh
dotnet test
```

The tests need the Compose Postgres running. They use their own database, `claims_test`, and switch all Kafka work off, so Kafka doesn't need to be up. There are no Angular tests (out of scope).

## Demo script

The demo users are two Claimants (Aisha Rahman, Daniel Tan) and three Officers (Priya Nair, Marcus Lim, Siti Hassan). Use **Switch user** in the header to change who you are. Keep the API terminal visible: the simulated emails are printed there.

1. **Claimant submits.** Pick Aisha Rahman. Choose **Report a Claim** and enter any Policy Number, Market SG, Motor, today's date, a short description. She gets a Claim Number such as `CLM-SG-000001`, and the Claim shows as Submitted. The API console prints a simulated email: `Simulated email to Claimant ...: "We've received your Claim CLM-SG-000001"`.
2. **Officer picks it up.** Switch to Priya Nair. The Claim is in her Queue. Open it and pick it up; it moves to Under Review. Set a Reserve, say 5000. Request info with a question.
3. **Claimant answers.** Switch to Aisha. The Claim shows Info Requested and the question. Answer it. The Claim goes back to Under Review. Her timeline shows every step.
4. **Officer approves.** Switch to Priya. Open the Claim and approve it. Then open **Dashboard**: the SG row of Exposure counts the Claim as open, with SGD 5,000.00 in Open Reserves.
5. **Officer settles, and Exposure drops.** Open the Claim again and settle it. The Claim shows Settled at once. Go back to the Dashboard: the SG open count and Open Reserves drop a few seconds later, not at once (the Dashboard refreshes every 5 seconds). That lag is eventual consistency. The Queue and Claim screens read live data, while the Dashboard reads Reporting's read model, which is fed through Kafka. Approving and settling each email Aisha in the API console.

To see the conflict rule, open a Submitted Claim as two different Officers in two separate browser windows. Make one a private window, because the chosen demo user is shared through localStorage. Pick Priya in one and Marcus in the other, then click **Pick up** in both within 5 seconds (before the next refresh hides the button). The second gets a clear 409 message. Or, in Swagger, send the same pick-up call twice, once with each Officer's `X-User-Id` and `X-Role`.

## Architecture

**One API, three modules** (a modular monolith, [ADR-0001](docs/adr/0001-modular-monolith.md)). Each module is a folder in `src/Claims.Api`.

- **Claims** owns the Claim, its Info Requests and its Claim History, and every REST endpoint that reads or changes them.
- **Notifications** consumes events and writes simulated emails to a log table and the console.
- **Reporting** consumes events into its own read model and serves Workload and Exposure.

Notifications and Reporting never read the Claims tables. They only know what the events tell them. That is the seam a later split would follow.

**REST vs Kafka** ([ADR-0003](docs/adr/0003-rest-for-requests-kafka-for-events.md)). If someone is waiting for the answer, it's REST. Submitting, picking up and deciding are all REST calls that change the Claim and answer straight away. If nobody is waiting, it's an event on Kafka. Emails and the Dashboard figures are built from events. One topic, `claim-events`, is keyed by Claim id, so each Claim's events stay in order ([ADR-0005](docs/adr/0005-one-topic-keyed-by-claim.md)).

**Outbox** ([ADR-0004](docs/adr/0004-transactional-outbox.md)). A change to a Claim and its event are saved in one database transaction. A background worker publishes saved events to Kafka every second. REST requests never talk to Kafka, so a Kafka outage can't fail a Claimant's action, and a crash can delay an event but not lose it.

**Idempotent consumers.** Delivery is at-least-once, so an event can arrive twice. Each consumer records the event ids it has processed, in the same transaction as its own changes, and skips any it has seen. It commits its Kafka offset only after that transaction succeeds.

**Eventual consistency.** The Queue and Claim screens read live data. The Dashboard reads Reporting's read model, which lags the live data by a second or two. The screens poll every 5 seconds, so a change can take up to about 6 seconds to show on the Dashboard.

**Identity** ([ADR-0006](docs/adr/0006-trusted-identity-headers.md)). There is no login. The role picker sends `X-User-Id` and `X-Role`, and the API trusts them. Every authorisation rule is still enforced by the API and tested.

The full reasoning for every choice, and what each was chosen over, is in the [decision log](docs/decisions.md) and the [ADRs](docs/adr/).

## Assumptions

- **The six Markets** are Singapore (SG, SGD), Malaysia (MY, MYR), Hong Kong (HK, HKD), Thailand (TH, THB), Indonesia (ID, IDR) and the Philippines (PH, PHP). Each has exactly one currency. The currency comes from the Market and is never entered. Amounts in different currencies are never added together (D7).
- Policy Numbers are typed in and not verified (D8).

## Known limits

- The Angular app holds amounts as JavaScript numbers, so it is exact to about 15 significant digits. The API, the database and the Claim History timeline are exact (D73).
- The API creates the Kafka topic at startup in Development only. Elsewhere the topic must already exist.

## Troubleshooting

- **The Dashboard shows Claims that don't exist.** Reporting has heard events for Claims that this database doesn't hold, for example from another copy of the API using the same Kafka, or from a database that was reset while Kafka kept its data. Reset the local stack with `docker compose down -v`, then start again from **Run it**. This wipes all Postgres and Kafka data.
- **The API can't reach Postgres.** Check `docker compose ps` shows Postgres healthy on port 5433.

## What was left out

- Real authentication (OIDC/JWT), a Manager role, and reassigning Claims between Officers.
- Policy verification, coverage checks and policyholder lookup.
- Attachments and file uploads.
- Claimant withdrawal, reopening closed Claims, and timeouts on unanswered Info Requests.
- Partial or negotiated settlements, payments, and currency conversion.
- Real email delivery, and showing notifications in the UI.
- Time-based measures, such as average time to settle.
- Server push (SSE or SignalR); the UI polls.
- Containerising the API and frontend, deployment, and CI.
- Angular tests, and automated tests of the Kafka adapters.
- A separate project per module, and splitting any module into its own service.

## What's next

1. **Split out Reporting first.** It already has its own tables and only hears events, so it can move to its own service and database without changing Claims.
2. Real login (OIDC/JWT). Only the authentication scheme and `CurrentUser` change.
3. A Manager role, with reassignment of Claims.
4. Server push (SSE) instead of polling.
5. Policy lookup against the policy administration system.
6. Containers, CI and deployment.

## Repo layout

| Path | What |
|------|------|
| `AI_JOURNAL.md` | Log of how AI was directed, challenged and overridden during the build |
| `GLOSSARY.md` | Domain terms |
| `docs/agent-prompt.md` | The opening prompt given to the AI pair-programmer |
| `docs/decisions.md`, `docs/adr/` | Decision log and architecture decision records |
| `docker-compose.yml` | Postgres and Kafka for local runs |
| `src/Claims.Api` | The API: `Claims/`, `Notifications/`, `Reporting/` modules plus shared `Infrastructure/` |
| `tests/Claims.Api.Tests` | Unit and integration tests |
| `web/` | The Angular app |
