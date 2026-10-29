# 05: Info Requests

**What to build:** The Assigned Officer asks the Claimant a question on a Claim Under Review, which moves it to Info Requested. The Claimant sees the question on their Claim and answers it, which sends the Claim back to Under Review automatically. Only one Info Request can be open at a time, and past questions and answers stay on the Claim.

**Blocked by:** 03: Officer Queue and pick-up

**Status:** ready-for-agent

- [ ] `POST /api/claims/{id}/info-requests` (Assigned Officer) works only from Under Review and moves the Claim to Info Requested
- [ ] Asking while an Info Request is open returns 409
- [ ] `POST /api/claims/{id}/info-response` (owning Claimant) answers the open Info Request and moves the Claim to Under Review; another Claimant gets 404; with no open request it returns 409
- [ ] `GET /api/claims/{id}` includes every Info Request with question, answer and times
- [ ] Both actions record Claim History entries and write InfoRequested (Claimant id, question) and InfoProvided to the outbox
- [ ] Unit tests cover asking, answering, the one-open rule, and refusal from the wrong status
- [ ] An integration test covers the round trip from question to answer
