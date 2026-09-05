# The report contract

Type: grilling
Status: open
Blocked by: (none — 01 resolved)

## Question

The JSON report is the canonical, framework-agnostic form of a selection — every dialect
renders from it, and PRD §4.3 is blunt about why it matters: "without it nobody can debug
a surprising result, and a tool nobody can debug gets switched off." PRD §12 makes
debuggability a success criterion. So the schema is a product surface, not an
implementation detail.

**What it must contain.** PRD §4.3 lists the selected tests, the total test count, the
changed methods that drove each selection, frameworks detected without a model, and
projects that fell back to coarse selection. Charting added more: per-test-project
grouping so a pipeline can skip projects entirely; the tier each selection came from; and
edge provenance along the path, so a selection can be explained hop by hop (ADR-0004).

**Open questions:**

- Is the full reverse path recorded for every selected test, or only the changed method
  that reached it? Full paths are what make surprises explicable and could be very large.
- How are *unselected* tests represented — by omission, by count, or explicitly? PRD §4.3
  says the report covers "what was not" selected.
- What does the report say when the answer is "nothing was selected"? That is the result
  most likely to be disbelieved.
- Does it carry build and test durations? Deferred here from charting: PRD §11's
  build-to-test ratio measurement is out of scope as a milestone, but if the report
  carries the numbers, the ratio comes free on first real use.
- Schema versioning, so consumers can depend on it while it evolves.
- Where output goes: stdout, a file, a file per project; and how the rendered filters are
  delivered alongside the JSON.
- **Exit codes and the error taxonomy.** PRD §8's table specifies behaviour for a failed
  build, a missing assembly, an unparseable project graph, and a thrown framework model.
  Each needs a distinct, documented exit code so a pipeline can tell "select nothing
  because nothing was affected" from "select nothing because Reach failed" — conflating
  those is how the invariant gets violated in practice.

**Carried in from the dialect research:** `--ignore-exit-code 8` — the usual way to stop a
zero-match run failing the build — **stops working on the .NET 11 SDK**, where zero-match
handling moved to a run-level verdict. Emitting no command at all for an empty selection is
the only version-independent answer, which makes "how is an empty selection delivered" a
contract question rather than a rendering detail.

**Carried in from ticket 16:** Microsoft's own MTP work delivers a selection as a
**test-node UID list** (`--filter-uid` / `TestNodeUidListFilter`) rather than a filter
string, and that path has no command-line length limit — which would dissolve the length
ceiling entirely for MTP hosts. The catch to weigh: UIDs come from the platform's own
discovery, so obtaining them may require a discovery pass Reach does not otherwise run.
Decide whether MTP renders as UIDs or as a filter string, and what that costs.

The dialect research is resolved; this ticket is now unblocked.
