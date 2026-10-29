# 11: Dashboard

**What to build:** An Officer opens a dashboard showing each Officer's Workload, the unassigned count, and Exposure per Market in its own currency. It's the visible proof that the Kafka-fed read model works. First to cut if time runs short; Swagger can stand in for it in the demo.

**Blocked by:** 08: Angular shell and role picker; 07: Workload and Exposure read model

**Status:** ready-for-agent

- [ ] Workload table: per Officer, Under Review / Info Requested / Approved / Settled / Rejected counts, plus the unassigned count
- [ ] Exposure table: per Market, open Claim count and total Reserve with its currency
- [ ] Polls every 5 seconds
- [ ] A short note on the screen that figures can lag live data by a few seconds
