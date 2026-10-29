# 07: Workload and Exposure read model

**What to build:** Any Officer can call the Workload and Exposure endpoints and see current totals that catch up within seconds of a change. Reporting builds them only from Kafka events, keeping its own per-Claim table, and never reads the Claims tables. A duplicated event is never counted twice, and Exposure never mixes currencies.

**Blocked by:** 04: Officer assesses and decides; 05: Info Requests; 06: Events reach Kafka; Claimants get simulated emails

**Status:** ready-for-agent

- [ ] The Reporting consumer uses its own consumer group and keeps one row per Claim (Claim id, Market, currency, status, Reserve, Assigned Officer), updated from every event type
- [ ] It records processed event ids in the same transaction as its update and skips repeats
- [ ] `GET /api/reports/exposure` (Officer only) returns, per Market, the open Claim count and total Reserve in that Market's currency, excluding Settled and Rejected Claims
- [ ] `GET /api/reports/workload` (Officer only) returns, per Officer, open Claims split by Under Review, Info Requested and Approved, plus Settled and Rejected counts, and the count of unassigned Submitted Claims
- [ ] Reporting reads nothing from the Claims module's tables
- [ ] Seam-3 tests (apply-event method, test database, no Kafka): the same event applied twice counts once; Exposure excludes Settled and Rejected and keeps Markets separate; Workload counts move correctly through a full lifecycle
- [ ] Manual check: settle a Claim and see Exposure drop a moment later
