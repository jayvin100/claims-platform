# 03: Officer Queue and pick-up

**What to build:** An Officer opens their Queue (unassigned Submitted Claims plus their own open Claims, oldest first, live data) and picks up a Claim, becoming its Assigned Officer and moving it to Under Review. If two Officers pick up the same Claim at once, one succeeds and the other gets a clear 409. Claimants can't reach Officer endpoints.

**Blocked by:** 02: Claimant submits and tracks a Claim

**Status:** ready-for-agent

- [ ] `GET /api/queue` (Officer only) returns unassigned Submitted Claims plus the caller's Claims that are neither Settled nor Rejected, oldest first, read live from the Claims tables
- [ ] Claims held by other Officers are not in the Queue
- [ ] `POST /api/claims/{id}/pick-up` assigns the caller and moves the Claim to Under Review, returning the updated Claim
- [ ] Picking up a Claim that isn't Submitted returns 409 with a problem-details message naming the rule
- [ ] The Claim uses Postgres `xmin` as its concurrency token; a concurrent save conflict returns 409
- [ ] A Claimant calling any Officer endpoint gets 403
- [ ] Pick-up records a Claim History entry and writes ClaimPickedUp (with Assigned Officer id) to the outbox
- [ ] Unit tests cover pick-up from Submitted and refusal from every other status
- [ ] Integration tests cover the two-Officer race (one 200, one 409) and the Claimant 403
