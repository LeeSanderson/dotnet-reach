# Reach persists no state between runs

Reach's inputs are a git baseline and a build output, and nothing else. It writes no
index, no cache and no history that a later run reads. This is what makes it installable
on an unfamiliar repository without an infrastructure ask (PRD §9.1) and what makes graph
staleness impossible by construction (PRD §9.3); stating it as an invariant rather than
as two separate scope decisions stops state creeping back in one convenience at a time.

## Consequences

PRD §4.2 lists "failed in the previous run" as a selection rule. That rule requires state
from a previous run, so it is out — **the PRD needs amending**. A pipeline that wants the
behaviour can union its own retry list with Reach's selection, which is where that
information already lives.

Local mode's in-memory call graph cache (PRD §7) is not a violation: it dies with the
process and no later run reads it.
