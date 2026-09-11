# What documentation M1 ships

Type: grilling
Status: claimed
Blocked by: (none — 03, 04, 07, 08, 19 resolved)

## Question

Graduated from the fog. PRD §12 makes **"a competent engineer can add Reach to an
unfamiliar pipeline in under an hour, using only documentation"** a success criterion for
M1, so documentation is in scope and is a *gate*, not a nicety. Nothing about its shape is
decided. Meanwhile the resolved tickets have been accumulating named obligations with
nowhere to land, and [the limitations register](19-the-limitations-register.md) has
already graduated out of this fog as its own document —
[docs/limitations.md](../../../docs/limitations.md) — because ADR-0008 made it
load-bearing. So M1 has one document already, and this ticket decides the rest.

**What is the set of documents, and who is each one for?** The obligations below are not
all addressed to the same reader. An adopter wiring a pipeline in under an hour, an
engineer staring at a surprising selection, and a consumer parsing `report.json` want
different documents. Decide the set and its shape before deciding what goes in each.

**The obligations already banked.** Each was named by a resolved ticket and deferred here:

- A **schema reference** for `report.json`, carrying the compatibility promise PRD v0.2
  committed to ([the report contract](07-the-report-contract.md)).
- **One page per notice code**, with `data` being free-form per code, documented per code
  (same ticket). Note this overlaps the register, which is already keyed by notice code —
  decide whether that is one surface or two.
- The **exit-code table**, and the one line of pipeline guidance that falls out of it:
  *non-zero means run the whole suite*.
- **A CI recipe per provider whose first line is the fetch-depth setting.** Non-negotiable:
  [ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)
  means Reach does not work under default CI settings, so exit 4 is the likeliest first run
  anyone has. Plus a TeamCity recipe passing `--base` explicitly, since TeamCity does not
  pass the value to the build process.
- **Install guidance leading with `dnx`, not `-g`** ([tool packaging](03-tool-packaging-and-cli-library.md)).
- **One line stating `.reach/`'s lifecycle is the caller's** ([the CLI surface](08-cli-surface.md)).
- **The `--no-build` asymmetry**, which inverts what people expect and so belongs in
  documentation rather than only the spec ([change-set edge cases](04-change-set-edge-cases.md)).
- **`--runsettings` documented by its effect** rather than its mechanism (CLI surface).

**Does the adoption-cost criterion get measured, or only asserted?** It is written as a
success criterion, but unlike the correctness criterion — which [the limitations
register](19-the-limitations-register.md) turned into a build failure — there is no
mechanism proposed for it. Decide whether M1 owes one, and if so what a cheap one looks
like, or state plainly that this criterion is judged rather than tested.

**What does the repository's own README owe?** Distinct from the adopter documentation:
the first thing a reader lands on, and the thing that has to survive Reach being
pointed at its own repository.

**What is explicitly not written in M1?** The counterpart to the out-of-scope section: the
docs that would be nice and are deferred, so an implementation ticket does not quietly
expand into a documentation site.

Resolve at 80/20, per the map's Notes. The bias should be toward the smallest set of
documents that discharges the banked obligations and clears the adoption-cost bar, with the
rest named and deferred.
