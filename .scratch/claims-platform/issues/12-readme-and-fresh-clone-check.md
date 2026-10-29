# 12: README and fresh-clone check

**What to build:** Someone who has never seen the repo can clone it, follow only the README, and have the platform running with the demo working. The README also explains the design, what was left out and what's next, pointing at the ADRs and decision log rather than repeating them.

**Blocked by:** All tickets that were built (01–11, minus any cut)

**Status:** ready-for-agent

- [ ] README: prerequisites (.NET 10 SDK, Node, Docker), the few commands to run everything, how to run tests, and a short demo script
- [ ] README: architecture summary, REST vs Kafka and why, eventual consistency, and links to ADRs and `docs/decisions.md`
- [ ] README: what was left out, and what's next (with the first service split being Reporting)
- [ ] README states the assumption about which six Markets
- [ ] Fresh-clone check: clone into a temp folder, follow only the README, confirm the API, Kafka flow, tests and Angular app all work; fix the README for anything that didn't
