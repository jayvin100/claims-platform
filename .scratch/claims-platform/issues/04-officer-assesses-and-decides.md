# 04: Officer assesses and decides

**What to build:** The Assigned Officer sets and revises a Reserve while the Claim is Under Review, then approves it (at that Reserve) or rejects it with a reason, and finally settles an Approved Claim. Settled and Rejected are final. No other Officer can act on the Claim. Each action appears on the Claimant's timeline and writes its event to the outbox.

**Blocked by:** 03: Officer Queue and pick-up

**Status:** ready-for-agent

- [ ] `PUT /api/claims/{id}/reserve` sets or revises the Reserve (amount only, currency from the Market); only while Under Review; amount must be greater than zero (400 otherwise)
- [ ] `POST /api/claims/{id}/approve` works only from Under Review with a Reserve set
- [ ] `POST /api/claims/{id}/reject` works only from Under Review and requires a reason
- [ ] `POST /api/claims/{id}/settle` works only from Approved
- [ ] Any action from the wrong status returns 409 with a message naming the rule; Settled and Rejected allow nothing further
- [ ] An Officer who isn't the Assigned Officer gets 403 on every action
- [ ] Each action records a Claim History entry and writes its event to the outbox: ReserveSet (amount, currency), ClaimApproved, ClaimRejected (reason), ClaimSettled, each with Claimant id where the spec lists it
- [ ] Each endpoint returns the updated Claim
- [ ] Unit tests cover every allowed and blocked transition and rule above
- [ ] Integration tests cover the other-Officer 403 and one happy path from submit to settle
