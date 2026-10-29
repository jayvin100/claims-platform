# Codex review

Every code review that dispatches Claude review agents also runs a Codex review of the same diff, in parallel (AGENTS.md). Codex is a second model with no shared context, so it isn't anchored on Claude's reading.

## Command

Run from the repo root. Write the prompt to a scratch file first, then:

```sh
codex exec -m gpt-6.1-sol -c model_reasoning_effort="high" -c service_tier="fast" \
  -s read-only -C "$(pwd)" --color never -o <scratch>/codex-review-<ticket>.md - < <scratch>/codex-review-<ticket>-prompt.md
```

- `-m gpt-6.1-sol`: GPT 6.1 Sol (slug from `~/.codex/models_cache.json`).
- `-c model_reasoning_effort="high"`: Jay's choice; the local config defaults to `xhigh`.
- `-c service_tier="fast"`: fast mode. The CLI sends it as the `priority` tier; the session log under `~/.codex/sessions/` shows `"service_tier":"priority"`.
- `-s read-only`: Codex can read files and run git, but can't write anything.
- `-o <file>`: the CLI saves Codex's final message there.
- `-`: read the prompt from stdin.

A review takes one to two minutes.

## What the prompt contains

1. The commit (or range) and its base, with `git diff <base>..<head>`.
2. Read-only rules: no edits, commits, stashes or checkouts.
3. Yardsticks: the ticket's acceptance criteria, the parent spec #2, `docs/decisions.md`, and the standards in AGENTS.md, `docs/agent-prompt.md` ("How we work (strict)"), GLOSSARY.md and `docs/adr/`. Jay must defend every line, so simplicity counts.
4. What is out of scope (the later tickets' work), so it isn't reported as missing.
5. Ignore generated migration Designer and snapshot files.
6. Report format: separate **Spec** and **Standards and correctness** sections, under 600 words, file:line for every finding, each marked hard issue or judgement call. Say so if a section has no findings.
7. The bodies of the ticket and #2, pasted at the end (`gh issue view <n> --json body -q .body`), because the sandbox has no network.
