# 02: Claimant submits and tracks a Claim

**What to build:** A Claimant submits a Claim (Policy Number, Market, Claim Type, incident date, description) and immediately gets back a Claim Number and a Submitted Claim. They can list their own Claims, newest first, and open one to see its details and Claim History timeline. Another Claimant's Claim is invisible to them. Every submission also writes a ClaimSubmitted event to the outbox in the same transaction. This ticket establishes the Claim entity, the outbox table and the test patterns that later tickets follow.

**Blocked by:** 01: Runnable skeleton with demo identity

**Status:** ready-for-agent

- [ ] `POST /api/claims` (Claimant only) returns 201 with the Claim, including a Claim Number in the form `CLM-<market>-<6 digits>` from one shared database sequence
- [ ] Currency is set from the Market (SG/SGD, MY/MYR, HK/HKD, TH/THB, ID/IDR, PH/PHP) and never accepted as input
- [ ] Missing or invalid fields return 400; an incident date in the future returns 400
- [ ] `GET /api/claims` returns only the caller's Claims, newest first
- [ ] `GET /api/claims/{id}` returns the Claim with its Claim History; a Claimant asking for another Claimant's Claim gets 404; an Officer can read any Claim
- [ ] The Claim changes only through its public methods; submit records a Claim History entry and raises ClaimSubmitted
- [ ] The outbox row (event id, type, Claim id, JSON payload, time) is saved in the same transaction as the Claim
- [ ] ClaimSubmitted carries event id, Claim id, Claim Number, Market, time, Claimant id and currency
- [ ] Unit tests (seam 2) cover submit and the future-date rule
- [ ] Integration tests (seam 1) cover submit, the 404 ownership rule, and the outbox row being written
