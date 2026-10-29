# 09: Claimant screens

**What to build:** A Claimant reports a Claim through a form, sees all their Claims in a list, and opens one to follow its timeline, read the Officer's question, answer it, and see the decision. The screens refresh themselves every few seconds.

**Blocked by:** 08: Angular shell and role picker; 02: Claimant submits and tracks a Claim; 05: Info Requests

**Status:** ready-for-agent

- [ ] Hand-written typed service for the Claimant endpoints
- [ ] My Claims list (Claim Number, type, Market, status), newest first
- [ ] Submit form (Policy Number, Market, Claim Type, incident date, description) showing validation errors in plain words, then the new Claim Number
- [ ] Claim detail: details, Claim History timeline, Info Requests with answers, the Reserve if set, and the rejection reason if Rejected
- [ ] An answer box appears only when an Info Request is open
- [ ] List and detail poll every 5 seconds
