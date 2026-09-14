# The documentation set and the committed example report

Status: ready-for-agent
Depends on: 18, 19
Spec: [§16.4](../../walking-skeleton/spec.md#164-documentation-ci-and-publishing)

## Goal

PRD §12 makes *"a competent engineer can add Reach to an unfamiliar pipeline in under an hour,
using only documentation"* a success criterion, so documentation is a **gate**, not a nicety. Four
documents, one of which already exists, and **no new machinery**.

## Scope

Three readers, three documents, plus the register — which is why the banked obligations never
fitted a single document.

| Document | Reader | Contents |
|---|---|---|
| **`README.md`** | someone deciding whether Reach is for them | pitch, a two-line quickstart, **the pipeline rule inlined**, then links |
| **`docs/adopting-reach.md`** | someone wiring a pipeline against the under-an-hour clock | install, CI, **the exit-code table**, and the switches whose behaviour surprises |
| **`docs/report-schema.md`** | someone parsing `report.json`, and someone staring at a selection they do not believe | field reference, **the complete notice-code index**, the compatibility promise, and **"Reading a surprising selection"** |
| **`docs/limitations.md`** | someone deciding whether to trust a selection | **already exists — unchanged in shape.** Reconcile its entries against the shipped notice catalogue only |

**No CLI reference document** — eleven options are `--help`'s job, and a second copy would drift
from the parser, which is the thing that is actually true. **No "how Reach decides" explainer** —
one README paragraph is it. Both are **dissolved rather than deferred**; do not re-add them.

### The pipeline rule is inlined, not linked

***If Reach exits non-zero, run the whole suite or stop the build.*** It appears in the README as
well as in the exit-code table. **This is the one deliberate duplication in the set**: it is the
single line whose absence silently violates PRD §8's invariant, and a reader who never opens the
adoption guide must still meet it. Everything else in the README is a link.

### One index for notice codes, in the schema reference

This settles a disagreement between two of the design tickets, so get the split right. The
**register keeps its identity** and holds accepted *holes*, each with a direction of failure.
**`docs/report-schema.md` carries a complete code index** — the full entry (meaning, `data` keys)
for codes that are **not** limitations, and one line plus a link for codes that are. Both cannot
document everything: `no-changed-member-reached-a-test` is an outcome, not a hole, and the `scope`
kind is mostly facts about a run.

A consumer gets one place enumerating the whole set. Blind-spot codes appear twice — index line
plus zoom. **This narrows "one page per notice code" to one index entry per code**: dozens of codes
do not warrant dozens of pages, and the codes carrying a real explanation are exactly the ones
already carrying it in the register.

**Do not add a second parity test for the index.** That would be a build failure guarding a
document nobody has broken yet; the blind-spot parity test is untouched.

### A committed example, not a JSON Schema file

The schema is deliberately **tolerant** — unknown fields ignored, selection sections optional,
`data` free-form, `schemaVersion` a single integer whose promise is that changes are additive. A
JSON Schema document is a poor instrument for that contract: it would have to be permissive
everywhere the promise lives, and it becomes a second artifact to hold in sync with the code.

**Commit an example `report.json` that is the output of ticket 19's determinism fixture.** The
determinism test that already exists then pins the documentation too — a worked example that
cannot rot, bought with a file. Revisit trigger for a real JSON Schema: *a consumer asks to
validate, or the first `schemaVersion` increment*.

### "Reading a surprising selection" is a section, not a document

PRD §12 requires a surprising selection to be explicable from the report **without rerunning the
tool** — which is precisely why an `explain` verb is out of scope, and which makes documentation
the thing that has to make it true. The section walks the committed example from one selected test
back through its `rules`, its path class and the change that reached it. A separate page would
restate half the schema to be legible, and then disagree with it.

### The obligations to discharge, each in exactly one place

- **Install guidance leads with `dnx dotnet-reach@<version>`, never `-g`.** The **version pin is a
  requirement, not a preference**: `dnx` with a bare package id will not find a prerelease and
  fails with a misleading "not found in NuGet feeds". The docs must not drift to the bare form.
- **The exit-code table**, in the adoption guide, with the pipeline rule.
- **Exit 8 means a Reach rendering bug and nothing else**, because no path through Reach's design
  renders a filter matching nothing — and **Reach never recommends `--ignore-exit-code 8`**, which
  does not merely suppress the signal but erases the evidence. Word it for current MTP hosts rather
  than as a promise: zero-match handling moves to a run-level verdict on the .NET 11 SDK.
- **One complete GitHub Actions recipe plus a table with a row per provider**, giving each
  provider's fetch-depth setting and detection variable — `fetch-depth: 0`, `fetchDepth: 0`,
  `GIT_DEPTH: 0`, `clone: depth: full`, and TeamCity's explicit `--base`, since TeamCity cannot be
  detected. **This deliberately narrows an earlier commitment to six recipes**: ADR-0010 already
  *is* that table, traps included, and five more recipes would be five copies of three steps, none
  of them verifiable — **an unverified recipe is worse than a table row because it looks
  authoritative.**
- **`.reach/`'s lifecycle is the caller's** — one line.
- **The `--no-build` asymmetry**: the mode that trusts the caller more is the mode that detects
  more. It inverts what people expect, so it belongs here and not only in the spec.
- **`--runsettings` documented by its effect**, not its mechanism.
- **The namespace/directory move asymmetry** from change detection, which will surprise someone.
- **The PRD stops being the manual.** The four facts an adopter needs — static analysis, compiled
  assemblies, over-selects on purpose, never narrows — are **one README paragraph**. The PRD link
  stays, **relabelled as background**: why Reach exists and where it is going.

### Adoption cost is judged, not measured — with one carve-out

The correctness criterion became a build failure because the ADR-0008 bargain *depends* on it;
nothing rides on adoption cost that way, so a harness for it spends the budget in the wrong place.
**State plainly that this criterion is judged.**

**The carve-out**: ADR-0010 means Reach does not work under default CI settings, so **exit 4 is the
likeliest first run anyone has**, and its fix is a line of YAML in these docs. That fenced block
must be **the same YAML Reach's own CI runs**. Ticket 22 owns the workflow and the parity test;
this ticket writes the block against the shape ticket 22 will ship, and the two land together.

## Acceptance criteria

- All four documents exist; the register is reconciled against the shipped notice catalogue with no
  entries removed.
- The example `report.json` is committed and is **the byte-for-byte output of ticket 19's
  determinism fixture** — asserted by a test, so it cannot rot.
- The notice-code index covers **every** code in the catalogue, with limitations linked rather than
  restated.
- The pipeline rule appears in both the README and the exit-code table, and nowhere else.
- The quickstart's `dnx` line carries a **version pin**.
- Relative links between the four documents resolve on GitHub, which is where a NuGet page lands
  you.
- The README's PRD link is labelled as background.
- "Reading a surprising selection" walks the committed example end to end.

## Out of scope

Deferred and **named**, so this ticket cannot quietly grow into a documentation site: a
documentation site (in-repo Markdown reads fine on GitHub); a JSON Schema file; five of the six CI
recipes in full (trigger: the first adopter on that provider, which is also the first time one
could be verified); a troubleshooting or FAQ page; contributor documentation; a migration guide
(nothing to migrate from at `schemaVersion` 1).
