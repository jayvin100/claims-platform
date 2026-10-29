# AGENTS.md

Take-home assessment: a claims platform (Angular + ASP.NET Core + Kafka). Jay must defend every line, so keep code simple and explain non-trivial pieces.

## Working rules

Read "How we work (strict)" in [docs/agent-prompt.md](docs/agent-prompt.md) before starting any task. It applies to every session, including after `/clear`. The rules agents most often miss:

- Update [AI_JOURNAL.md](AI_JOURNAL.md) at every commit. Take times from `date`, never guess them. Never invent Jay's reasons; ask him.
- In each ticket's journal entry, list every bug found (by a test, a review, or the AI itself) and how it was fixed, or why it was left (Jay's rule, Thu 29 Oct 12:57).
- Push to origin after each commit.
- Anything that needs Jay's decision (implementation calls for review, a new NuGet or npm package, a change to an agreed decision, a scope cut) goes to a council of Claude and Codex agents; their consensus is logged as the direction Jay approved (Jay's rule, Thu 29 Oct 13:06; see [docs/agents/council.md](docs/agents/council.md)).
- Warn Jay at 3h and 4h of build time. The session started at Thu 29 Oct 10:48 +08 (see the time log in the journal).
- Don't put client company names or logos in code, UI or docs.
- The lead session orchestrates: builder agents write tickets, fixer agents apply review fixes, and the lead dispatches reviews and councils, merges, and writes the journal (Jay's rule, Thu 29 Oct 13:17).
- Every code review that dispatches Claude review agents must also run a Codex review of the same diff, in parallel (Jay's rule, Thu 29 Oct 12:41; see [docs/agents/codex-review.md](docs/agents/codex-review.md)). Record each review's findings, and what was fixed or left, in the journal entry for that commit.

## Agent skills

### Issue tracker

GitHub Issues via `gh`. The wayfinder map is #1 (the single source of truth for the assignment); the spec is #2, a sub-issue of the map; tickets are sub-issues of the spec, with native blocking links. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default roles as GitHub labels, plus `wayfinder:*` labels on the map and decision tickets. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
