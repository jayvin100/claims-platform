# 10: Officer screens

**What to build:** An Officer works their Queue, opens a Claim, and moves it through pick-up, Reserve, Info Request, approval or rejection, and settlement, seeing only the actions the Claim's current status allows. If another Officer got there first, they see a plain message instead of an error dump.

**Blocked by:** 08: Angular shell and role picker; 03: Officer Queue and pick-up; 04: Officer assesses and decides; 05: Info Requests

**Status:** ready-for-agent

- [ ] Hand-written typed service for the Officer endpoints
- [ ] Queue: Claim Number, Market, type, status, age and a "mine" marker, oldest first
- [ ] Claim detail with details, timeline, Info Requests and Reserve
- [ ] Action controls shown only when allowed: pick up (Submitted), set Reserve, request info, approve, reject with reason (Under Review, Assigned Officer), settle (Approved, Assigned Officer)
- [ ] 409 and 403 responses are shown using the problem-details message
- [ ] Queue and detail poll every 5 seconds
