# Decision council

Jay's rule (Thu 29 Oct 13:06): anything that needs a decision from Jay goes to a council of Claude and Codex agents. They think, decide, discuss and reach a consensus. The consensus is logged as the direction Jay approved, through this standing delegation.

## What goes to the council

- A ticket's implementation calls (the D-rows an agent proposes for Jay's review). One council per ticket covers all its calls; each call is accepted or changed.
- A new NuGet or npm package.
- A change to an agreed decision (D-rows, ADRs, the spec), or a scope cut at the 3h and 4h warnings.
- An ASK item, or a stop in "How we work (strict)" that would otherwise wait for Jay.

Still Jay's own: anything outside the repo's normal work (deleting or renaming the repo, changing its visibility or settings, spending money), and anything Jay asks to decide himself. The 3h and 4h warnings still go to Jay.

## Members

- **Claude A:** a fresh agent with a simplicity lens: what Jay can defend line by line in a 30–60 minute panel, and what fits the time left.
- **Claude B:** a fresh agent with a correctness lens: the spec, the ADRs, production realism and risk.
- **Codex:** GPT 6.1 Sol, high reasoning, fast, read-only sandbox (same command as [codex-review.md](codex-review.md)).

None of them is the agent that built the work, so none defends its own code.

## Rounds

1. **Decide alone.** Each member gets the same brief: the decision and its options, the context files (spec #2, the ticket, `docs/decisions.md`, ADRs, GLOSSARY.md, the code), the constraints (the 5h cap and time left, Jay must defend every line, no client names), and any review findings. Each picks an option per question, with reasons, in under 250 words.
2. **Discuss.** Each member reads the other two positions and gives a final pick per question, with a one-line reason, saying what changed its mind if anything did.
3. **Consensus.** Unanimous, or two of three, wins; any dissent is recorded. If all three differ, the lead picks the option that is simplest and still consistent with the spec, and records that it did.

## Logging

- `docs/decisions.md`: the decision row, marked "council, approved under Jay's delegation".
- `AI_JOURNAL.md`: the question, each member's round-1 pick, any change in round 2, the outcome, and any dissent. Reasons are the council's, never invented as Jay's.
- Tell Jay the outcome in one line per decision, so he can still defend it.
