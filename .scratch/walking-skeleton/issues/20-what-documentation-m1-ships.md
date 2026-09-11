# What documentation M1 ships

Type: grilling
Status: resolved
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

## Answer

**Four documents, one of which already exists, and no new machinery.** M1 ships
`README.md`, `docs/adopting-reach.md`, `docs/report-schema.md` and the
[limitations register](../../../docs/limitations.md). Every obligation the resolved tickets
banked lands in one of them, each in exactly one place.

### The set, and the reader each one answers to

The obligations were not addressed to one reader, which is why they never fitted a single
document. Three readers, three documents, plus the register:

- **`README.md`** — someone deciding whether Reach is for them. Pitch, a two-line
  quickstart (`dnx` install, one `dotnet reach select`), and the pipeline rule. Then links.
- **`docs/adopting-reach.md`** — someone wiring a pipeline, working against PRD §12's
  under-an-hour clock. Install, CI, exit codes, and the switches whose behaviour surprises.
- **`docs/report-schema.md`** — someone parsing `report.json`, and someone staring at a
  selection they do not believe. Field reference, the complete notice-code index, the
  compatibility promise, and a worked trace.
- **`docs/limitations.md`** — already exists. Unchanged in shape by this ticket.

**No CLI reference document.** Eleven options are `--help`'s job; a second copy would drift
from the parser, and the parser is the thing that is actually true.

### The pipeline rule is inlined, not linked

*Non-zero means run the whole suite* appears in the README as well as the exit-code table.
This is the one deliberate duplication: it is the single line whose absence silently
violates PRD §8's invariant, and a reader who never opens the adoption guide must still
meet it. Everything else in the README is a link.

### One index for notice codes, in the schema reference

**This settles a disagreement between two resolved tickets.**
[The report contract](07-the-report-contract.md) said `data` is "free-form per code,
documented in the register", which assumes the register documents every code. [The
limitations register](19-the-limitations-register.md) scoped it to accepted *holes*, each
with a direction of failure. Both cannot hold: `no-changed-member-reached-a-test` is an
outcome, not a hole, and the `scope` kind is mostly facts about a run.

The register keeps its identity. **`docs/report-schema.md` carries a complete code index**,
holding the full entry — meaning, `data` keys — for codes that are not limitations, and one
line plus a link for codes that are. A consumer gets one place that enumerates the whole
set, which is the same argument ticket 07 used to reject per-entry notices in favour of one
global `notices` array. Blind-spot codes appear twice, as index line plus zoom, which is the
pattern this map itself runs on.

This also **narrows ticket 07's "one page per notice code"** to one index entry per code.
Dozens of codes do not warrant dozens of pages, and the codes that carry a real explanation
are exactly the ones already carrying it in the register.

The parity test is untouched: every `blind-spot` code must have a register entry, asserted
by Reach's own suite. Nothing here adds a second such obligation for the index — that would
be a build failure guarding a document nobody has broken yet.

### The schema reference is prose plus a committed example, not a JSON Schema file

Ticket 07 made the schema deliberately **tolerant**: unknown fields ignored, selection
sections optional, `data` free-form, `schemaVersion` a single integer whose promise is that
changes are additive. A JSON Schema document is a poor instrument for that contract — it
would have to be permissive everywhere the promise lives, and it becomes a second artifact
to hold in sync with the code, which is what
[the register ticket](19-the-limitations-register.md) declined to do in either direction.

**A committed example `report.json` is the better instrument, and it is nearly free.** [The
fixture catalogue](11-fixture-catalogue.md) already runs a fixture twice and compares the
two documents byte-for-byte to pin determinism. The committed example is *the output of
that run*, so the determinism test that already exists also pins the documentation. A worked
example that cannot rot, bought with a file.

### The debuggability criterion is a section, not a document

PRD §12 requires a surprising selection to be explicable from the report *without rerunning
the tool*. That is precisely why [an `explain` verb is out of scope](08-cli-surface.md) —
the report was declared sufficient, which makes documentation the thing that has to make it
true. **"Reading a surprising selection"** is a section of `docs/report-schema.md`, walking
the committed example from one selected test back through its `rules`, its `path class` and
the change that reached it. A separate page would restate half the schema to be legible, and
then disagree with it.

### Adoption cost is judged, not measured — with one carve-out

The correctness criterion became a build failure because ADR-0008's bargain *depends* on it;
nothing rides on adoption cost that way, so a harness for it spends the 80/20 budget in the
wrong place. Stated plainly rather than left ambiguous: **this criterion is judged.**

The carve-out is the one place where a documentation error breaks the criterion on contact.
[ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)
means Reach does not work under default CI settings, so **exit 4 is the likeliest first run
anyone has**, and its fix is a line of YAML in the docs. That block must be **the same YAML
Reach's own CI runs**, asserted by a test comparing the fenced block against the workflow
file. It is a constraint [ticket 21](21-ci-for-the-reach-repository.md) inherits, not new
machinery here.

### One verified CI recipe and a table, not six recipes

Six providers — five auto-detected by ADR-0010, plus TeamCity, which cannot be detected —
whose recipes differ by one line each: `fetch-depth: 0`, `fetchDepth: 0`, `GIT_DEPTH: 0`,
`clone: depth: full`, and TeamCity's explicit `--base`. M1 ships **one complete GitHub
Actions recipe**, the one pinned to the repo's own workflow above, **plus a table with a row
per provider** giving its fetch-depth setting and its detection variable.

**This narrows a commitment [the CLI surface](08-cli-surface.md) already made** — "a recipe
per CI provider whose first line is the fetch-depth setting" — and the narrowing is
deliberate, taken by the owner. ADR-0010 already *is* that table, traps included, so the doc
transcribes an existing decision rather than inventing five. Five more recipes would be five
copies of three steps, none of them verifiable, and an unverified recipe is worse than a
table row because it looks authoritative.

### The exit-code table lives with the person who acts on it

Exit codes and the run-level `outcome` are two renderings of one conclusion, but they are
not the same closed set — seven codes against four outcomes — and they serve different
readers. **`docs/adopting-reach.md` owns the table**; `docs/report-schema.md` states the
relationship in one line, *exit codes carry the outcome*, and links.

### The PRD stops being the manual

`README.md` currently closes with "See PRD.md for more details", which points an adopter at
a planning artifact: milestones, open questions and risks, non-goals, and a v0.1→v0.2
amendments appendix describing features that do not exist. The four facts an adopter
actually needs — static analysis, compiled assemblies, over-selects on purpose, never
narrows — are **one paragraph in the README**, which its existing second paragraph nearly
writes already. The PRD link stays, **relabelled as background**: why Reach exists and where
it is going. No "how Reach decides" explainer, because the reader who needs more than a
paragraph is the one debugging a selection, and the section above serves them.

### Not written in M1

Deferred, and named so an implementation ticket cannot quietly grow into a documentation
site:

- **A documentation site.** In-repo Markdown reads fine on GitHub, which is where a NuGet
  page lands you, and relative links between the four documents survive there.
- **A JSON Schema file.** Revisit trigger: *a consumer asks to validate, or the first
  `schemaVersion` increment* — the two moments where "tolerant and additive" stops being
  self-evident.
- **Five of the six CI recipes in full.** Revisit trigger: *the first adopter on that
  provider*, which is also the first time one could be verified.
- **A troubleshooting or FAQ page.** Nothing has generated one yet.
- **Contributor documentation.** Repo hygiene, not adopter documentation.
- **A migration guide.** Nothing to migrate from at `schemaVersion` 1.

Two are **dissolved rather than deferred**, recorded so nobody re-adds them: the **CLI
reference document** (`--help` is the reference) and the **"how Reach decides" explainer**
(the README paragraph is it).

The rest get no trigger. A site, an FAQ and a migration guide all announce their own absence,
and writing speculative triggers for them is the documentation equivalent of pre-slicing the
fog.

### No ADR, no glossary change

Considered and declined. The narrowings here — six recipes to one plus a table, a page per
code to an index entry — are cheap to reverse, which fails the first ADR test; they are
recorded above and gisted on the map, which is where a reader will look. No new domain terms
surfaced either: `notice`, `limitations register` and `report` already carry the vocabulary
this ticket needed, and `CONTEXT.md`'s note that a notice code "can be documented once and
depended on" is now literally true with an address.
