# CI for the Reach repository itself

Type: grilling
Status: open
Blocked by: (none — 10, 11, 13 resolved)

## Question

Graduated from the fog. This is the one ticket about Reach's *own* repository rather than
about the product: what runs on a push, and whether the tool is published anywhere during
M1. An implementation ticket cannot be written for it while the answer is unknown, and the
spec's acceptance criteria have nowhere to run without it.

**What does CI do on a push?** Build, test, and what else. [The fixture
catalogue](11-fixture-catalogue.md) settled that integration tests get their history from a
temp git repository per test, which makes them self-contained — but it also means CI needs
whatever `git` and SDK those tests assume.
[ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md) fixes the SDK floor at .NET
10, so the runner image and any `global.json` pin follow from it.

**Is the tool published during M1, and where?** NuGet, GitHub Packages, a GitHub release
artifact, or nowhere at all and consumers build from source. [Tool
packaging](03-tool-packaging-and-cli-library.md) decided the packaging and that install
guidance leads with `dnx` — which is install guidance for a *published* package, so the
two answers have to agree. If nothing is published in M1, that guidance is documentation
for a future state and [the documentation ticket](20-what-documentation-m1-ships.md) needs
to know.

**Does versioning need deciding now?** Only if something is published. If it is, the report
schema's compatibility promise (PRD v0.2) attaches to a version number, so the two are
linked.

**Does Reach run on Reach?** The obvious dogfooding move, and [project layout and
ports](10-project-layout-and-ports.md) leaned on it as an argument. Decide whether that is
an M1 CI step, a manual first-datapoint exercise —
[method identity](09-method-identity-and-performance-budget.md) deferred the performance
budget to "the first real run as the first datapoint" — or out of scope for now.

**What is the minimum that makes the implementation effort safe to hand off?** That is the
bar, not a complete pipeline. The destination is a spec plus implementation tickets for a
walking skeleton; CI earns its place here by unblocking those tickets, not by being
thorough.

Resolve at 80/20, per the map's Notes.

## Comments

**From [What documentation M1 ships](20-what-documentation-m1-ships.md),** which resolved
first and left two constraints here rather than deciding them:

1. **The GitHub Actions workflow is also a documentation artifact.** M1 ships one complete
   CI recipe, and it must be the same YAML this repository's own CI runs, asserted by a test
   comparing the fenced block in `docs/adopting-reach.md` against the workflow file. That
   ticket made adoption cost a *judged* criterion everywhere except here, because
   ADR-0010 makes exit 4 the likeliest first run anyone has and a wrong fetch-depth line in
   the docs breaks PRD §12's criterion on contact. So whatever CI does, it does it in a
   workflow file shaped to be readable as an example.
2. **Publishing is still open and the docs are waiting on it.** The README carries a
   two-line quickstart whose first line is a `dnx` install. If M1 publishes nothing, that
   line documents a future state and both the README and `docs/adopting-reach.md` need
   different wording — so this ticket's answer changes text that is already specified.
