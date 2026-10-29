# AGENTS.md

Take-home assessment: a claims platform (Angular + ASP.NET Core + Kafka). Jay must defend every line, so keep code simple and explain non-trivial pieces.

## Working rules

Read "How we work (strict)" in [docs/agent-prompt.md](docs/agent-prompt.md) before starting any task. It applies to every session, including after `/clear`. The rules agents most often miss:

- Update [AI_JOURNAL.md](AI_JOURNAL.md) at every commit. Take times from `date`, never guess them. Never invent Jay's reasons; ask him.
- Push to origin after each commit.
- Stop and ask before adding any NuGet or npm dependency, or before changing an agreed decision.
- Warn Jay at 3h and 4h of build time. The session started at Thu 29 Oct 10:48 +08 (see the time log in the journal).
- Don't put client company names or logos in code, UI or docs.

## Agent skills

### Issue tracker

Local markdown: specs and tickets live under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default roles, recorded as a `Status:` line in each ticket file. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
