# Walking Skeleton (M1)

Labels: wayfinder:map

## Destination

A **spec for M1** — the walking skeleton described in PRD §12 — plus ready-for-agent
implementation tickets, handed off for a separate implementation effort.

Planning only. This map produces decisions and a spec, not code. When a ticket makes you
want to start building, that is the edge of the map: hand off instead.

## Notes

- Product definition: [PRD.md](../../PRD.md). Vocabulary: [CONTEXT.md](../../CONTEXT.md).
  Decisions: [docs/adr/](../../docs/adr/). Read the glossary before writing anything and
  use its terms; where a decision contradicts the PRD, say so explicitly.
- **PRD.md is at v0.2** and already reflects every decision through ticket 07, so a
  contradiction found there is news rather than known staleness. Its closing appendix lists
  what v0.1 said and what superseded it. A fresh contradiction goes to
  [PRD amendments](issues/06-prd-amendments.md) — reopen it rather than editing the PRD
  under an unrelated ticket, since amending an approved document is the owner's call.
- Default skills for a ticket: `grilling` and `domain-modeling`, unless its `Type:` says
  otherwise.
- **The correctness rule outranks everything.** Under-selection is a correctness bug, not
  a tuning issue (PRD §8). Where a decision is genuinely uncertain, take the option that
  widens the selection.
- **Resolve at 80/20** — [ADR-0012](../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md).
  Where a question has a simple answer covering the common case and a complete answer
  covering every edge, take the simple one and write the constraint down. Owner decision,
  generalising ADR-0008 across the whole remaining map. It does **not** license leaving a
  question undecided: state the rule adopted, the case it does not cover, and the direction
  that case fails in. Simplifications that over-select are free; those that under-select owe
  a register entry, and a notice code wherever Reach can detect an instance.
- M1 ships **no framework models** (PRD §12). Whole-project fallbacks are not models and
  are in scope.

## Decisions so far

Taken while charting, before any ticket existed. Terse by design — the reasoning lives in
the ADR where one exists.

- **Change detection is Roslyn**: parse changed files at both revisions and hash method
  bodies, so comment and formatting churn does not select.
- **The join is a seam**: turning a changed declaration into a graph identity stays
  abstract, with signature keys and PDB line spans both live candidates, decided by a
  spike at implementation time.
- **Both build modes ship**: default invokes `dotnet build`; `--no-build` skips it.
- **Source-binary correspondence is verified in both modes** — [ADR-0003](../../docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md).
- **Widening through virtual and interface dispatch is in M1**, with a single-pass type
  hierarchy index (PRD §9.4).
- **Edges carry provenance** — [ADR-0004](../../docs/adr/0004-call-graph-edges-carry-provenance.md).
- **Test recognition covers xUnit (v2 and v3), NUnit and MSTest**; an unrecognised
  framework falls back to whole-project selection. *There is no xUnit v4* — the `xunit.v3`
  package is at version 4.0.0, and that release changed the filter surface, so dialects are
  gated on package version as well as framework and runner host.
- **NUnit filters render with `~` (contains), not equality.** NUnit's VSTest
  `FullyQualifiedName` includes a parameterised test's arguments, and equality matching
  works only through an adapter re-parse that several supported configurations disable.
  Reach cannot see a consumer's `.runsettings` and cannot detect the failure, so this
  follows the widen-when-uncertain rule.
- **The framework-agnostic JSON report is canonical**; Reach detects framework and runner
  per test project and renders every dialect from it.
- **Assembly discovery scans output directories**, not MSBuild — it has to work under
  `--no-build`. The solution file supplies the expected project list so a missing assembly
  is an error; first-party assemblies are identified by their debug symbols pointing at
  source inside the working tree.
- **The target may be a solution or a single test project** — [ADR-0002](../../docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md).
- **Every target framework of a multi-targeted project is analysed**, and the target
  framework is part of method identity.
- **Unmappable changes resolve in tiers**: a changed method walks the graph; a changed
  file with no changed method selects whole assemblies whose debug symbols list that
  document; a file no symbols reference hits a rule table, whose mandatory row is
  source-generator-to-consumers; MVID comparison is held as a later cross-check. The
  table's rows are owned by
  [The unmappable-change rule table](issues/15-the-unmappable-change-rule-table.md).
- **Baseline is the merge-base** by default, with an explicit ref available and a shallow
  clone a loud error. The change set spans committed-since-baseline, staged and unstaged
  changes. `git` is shelled out to, behind a port.
- **Testing is in-memory Roslyn compilation for the core** plus committed fixture
  solutions for integration, with every environment boundary behind a port.
- **PRD §4.2's rules 1, 2, 3 and 5 are in; rule 4 is out** — [ADR-0001](../../docs/adr/0001-reach-persists-no-state-between-runs.md).
- **The tool targets `net10.0`** with `RollForward: LatestMajor` —
  [ADR-0013](../../docs/adr/0013-the-tool-targets-net10-0.md). Supersedes the charting
  decision of `net8.0`; roll-forward is still needed, since the default policy will not
  cross a major version.
- **Selection granularity is the test method**, never the individual test case (PRD §11).
- **M1 accepts named under-selection** rather than widening for every hole —
  [ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md). A conscious owner
  override of the correctness rule in the Notes above, valid only while every accepted hole
  is named in the limitations register and surfaced in the report.

Resolved tickets:

- [What is dotnet test --affected-tests](issues/16-what-is-dotnet-test-affected-tests.md):
  Microsoft is building coverage-based test selection into `dotnet test`, behind an
  environment variable, in unreleased branches, with the engine in an unpublished private
  extension. It requires exactly the standing infrastructure PRD §9.1 rejected, so it is
  complementary and M1 proceeds unchanged — but it is more precise where Reach is weakest,
  and "no infrastructure ask" is a narrower moat than the PRD assumes.
- [Filter dialects and runner detection](issues/01-filter-dialects-and-runner-detection.md):
  a method-level filter does match every case of a parameterised test in all three
  frameworks, confirmed from adapter source. No xUnit v4. NUnit needs contains-matching to
  be safe. Surfaced `dotnet test --affected-tests` — ticket 16.
- [Solution and project-file parsing](issues/02-solution-and-project-file-parsing.md):
  `Microsoft.VisualStudio.SolutionPersistence` reads both `.sln` and `.slnx` with zero
  dependencies and no MSBuild, and `.slnx` is already the default for new solutions.
  Corrected ADR-0002's claim about `ReferenceOutputAssembly=false`. Output layout turns out
  to be undecidable from disk, and generator detection harder than assumed — tickets 14
  and 15.
- [Tool packaging and CLI library](issues/03-tool-packaging-and-cli-library.md):
  `System.CommandLine` 2.0.x — stable since .NET 10, zero dependencies on `net8.0`, and
  what the `dotnet` CLI itself is built on. `RollForward: LatestMajor` verified end to end
  against Microsoft's own `net8.0` sample tool. Install guidance leads with `dnx`, not
  `-g`. Surfaced the Roslyn conflict now held in ticket 13.
- [Change-set edge cases: deletions, renames, untracked files](issues/04-change-set-edge-cases.md):
  the tier ladder routes **per change, not per file**, and the results union. A deleted
  method widens its declaring type unconditionally — a deleted `override` or `operator ==`
  compiles clean and rebinds, so "no caller means inert" is unsafe; an absorption test that
  would prove most deletions inert is written down but deferred as five clauses of
  under-selection risk. A deleted *file* is routed as the deletion of every type it
  declared, with nearest-ancestor-project directory containment as the fallback for a type
  that is genuinely gone. Renames are never detected —
  [ADR-0005](../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md). Untracked
  files are always in, with no flag. Changes outside the analysis scope are reported with a
  category and never selected on, and **an empty selection is never emitted without a reason
  list**. Ignored-and-untracked compiled files are a blind spot Reach detects and reports but
  cannot select on. Added terms `whole-assembly widening` and `whole-type widening`; surfaced
  ticket 18.
- [The report contract](issues/07-the-report-contract.md): one report per run keyed on
  **(test project, target framework)**, each entry holding a mode and an **`invocations`
  array** — zero invocations for an empty selection, so "emit no command" is the only
  representable answer rather than a rule to remember. The change side is keyed on **changes,
  not expanded roots**, because whole-assembly widening would otherwise put ten thousand roots
  on every test; the tier lives on the change. Each selected test carries the **rules** that
  selected it (PRD §4.2 has four, and only reverse-reachability had been designed for) and, per
  change, a **path class** — the weakest edge on the strongest path. A forward list of every
  change with the count of tests it reached makes an inert change readable rather than absent.
  One global **notices** array with stable kebab codes and one `kind`; every `blind-spot` code
  must have a limitations-register entry, asserted by a test, which makes ADR-0008 mechanical.
  Rich exit codes with **an empty selection at zero**, so "non-zero means run everything" is the
  invariant in one line; a report is written even on failure. Delivery uses each host's private
  file channel and **chunks for VSTest under `dotnet test`** rather than writing a
  `.runsettings` — [ADR-0009](../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md).
  Found two gaps: the rendered NUnit `~` filter **runs more tests than the report says were
  selected**, so both counts are carried or ticket 17 measures the wrong number; and adding
  `[Fact]` to an existing method creates a test with an unchanged body, escalated to ticket 18.
- [Generics, delegates and function pointers in the graph](issues/05-generics-delegates-and-function-pointers.md):
  a node is an **IL method definition**, per assembly, per target framework, with generic
  instantiations collapsed and accessors as nodes in their own right —
  [ADR-0006](../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md). Seven edge
  kinds, in three safety classes rather than two, which amended
  [ADR-0004](../../docs/adr/0004-call-graph-edges-carry-provenance.md). Widening is bounded
  by the **inferred receiver type**, because Roslyn emits `callvirt` against the
  slot-defining declaration and the naive reading fans every `ToString()` call site out to
  the whole suite —
  [ADR-0007](../../docs/adr/0007-widening-targets-the-inferred-receiver-type.md). PRD §9.2
  understates two holes: an `async` body is reachable only through the BCL, closed by a
  **containment edge** from the kernel method, and first-party members the host calls back
  into are not reachable at all, left open and named. Every IL claim verified by
  disassembly, and two working assumptions were overturned in the process.

- [PRD amendments](issues/06-prd-amendments.md): **every amendment accepted, nothing rejected**,
  so no ADR needed revisiting. PRD.md is now **v0.2** with an appendix tabulating each change
  against the record that drove it. The owner call that mattered: ADR-0008's override is written
  in as a **bounded exception in a new §8.1** — it holds only while every hole is in the register
  and surfaced in the report, and lapses with the register — which gave §12 a second correctness
  criterion for M1, since shadow mode is M2's and cannot measure it. Rule 4 removed outright
  rather than restated as a pipeline union, so nothing invites a consumer to depend on behaviour
  Reach does not implement. Four contradictions outside the checklist were found and fixed;
  §4.2 rule 3 now records its dependency on attribute-level detection without pre-empting
  ticket 18. Two new commitments: the report schema carries a compatibility promise, and §11
  holds a revisit trigger for the coverage argument.

- [CLI surface](issues/08-cli-surface.md): one verb, one positional, eleven options.
  `dotnet reach select [<SOLUTION|PROJECT>] [options] [-- <dotnet build args>]`. The verb is
  **required** — bare `dotnet reach` prints help and **exits 1**, because the default mode
  invokes `dotnet build` and a newcomer typing the tool's name must not trigger a multi-minute
  build; and because exiting 0 with no report would hand a mis-wired pipeline "success". Two
  findings turned out to be environmental rather than matters of taste, and both became ADRs.
  **Reach does not work under default CI settings** —
  [ADR-0010](../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md):
  `actions/checkout` fetches one refspec at depth 1, so `merge-base` has neither the ref nor the
  history, and `refs/remotes/origin/HEAD` does not exist after *any* CI checkout because neither
  provider runs `git clone` — so the obvious default-branch fallback is unavailable offline. The
  fix is one line of YAML, which makes exit 4 the likeliest first run anyone has and its message a
  designed artifact that prints the literal line. Baseline collapses to **one** option, `--base`,
  always merge-base, auto-detected through five CI variables (not TeamCity, which does not pass
  the value to the build process). **A solution wins over a project in discovery** —
  [ADR-0011](../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md) —
  deliberately diverging from MSBuild's base-name rule, because choosing the project is choosing
  the narrower scope; `.slnf` is rejected for the same reason. That draws the boundary ticket 14
  inherits: **mirroring `dotnet build` governs option spellings, not resolution semantics.** Added
  `--` passthrough to the build, without which every non-trivial adopter is stuck on `--no-build`,
  with layout-affecting switches refused in the passthrough. No dry-run verb: a normal run already
  is the preview, which puts the weight on the summary — resolved baseline SHA first, the two
  zero-outcomes visibly distinct, over-selection as a percentage.

- [Tool target framework versus the Roslyn dependency](issues/13-tool-target-framework-versus-roslyn.md):
  **the tool targets `net10.0`**, single TFM, latest Roslyn —
  [ADR-0013](../../docs/adr/0013-the-tool-targets-net10-0.md). Owner decision against the
  ticket's recommendation of `net8.0`; the accepted cost is that Reach does not run on an agent
  carrying only .NET 8, since roll-forward never goes downward. `RollForward: LatestMajor` is
  **kept**, because the default policy will not cross a major version either. The packaging
  conflict dissolves rather than being traded: Roslyn 5.9.0 ships a native `net10.0` asset, so
  there is no facade chain and no `NU1605` class. The fact-find also disposed of the option the
  ticket flagged for investigation — the `netstandard2.0` asset is the *same* front-end
  retargeted, verified identical in public surface and byte-identical in parse output across
  C# 1–14 — so lowering the floor later is a csproj edit, not a redesign. The half that mattered
  more than the TFM: **Roslyn checks language versions at binding, not parsing**, so a
  parse-only tool gets no "feature unavailable" diagnostic and **a clean parse never proves a
  correct parse** — an older parser meeting `record` produced a *method* named `Person` with
  zero diagnostics. Hence: parse with `LanguageVersion.Preview`; an error diagnostic *or*
  skipped-tokens trivia widens the project; a `LangVersion` above Reach's ceiling gets a notice
  but not widening; the silent misparse is a register entry.
- [Method identity and the performance budget](issues/09-method-identity-and-performance-budget.md):
  identity is a **64-bit value** — 1 discriminator bit, 31-bit assembly-instance ordinal, 32-bit
  metadata token. **The TFM needs no field**: an assembly instance *is* one (project, TFM) pair,
  so multi-targeting falls out of the ordinal and the hottest struct stays 8 bytes. The
  discriminator exists because ADR-0006 needs widening anchors for declarations Reach never
  reads — `object::ToString`, `IDisposable::Dispose` — which have no token, so they are interned
  external references instead. Cross-assembly resolution is a **one-time string-keyed index per
  assembly**, which is what PRD §9.4 actually permits: it forbids strings in *identity*, not in a
  build-once lookup. Resolution failure has three different right answers, and ambiguous
  signatures **edge to every candidate**. Edges are a flat triple array inverted into a CSR
  reverse index *after* the pass; only the reverse index is built. **No committed wall-clock
  number in M1** (owner decision) — instead the two §9.4 constraints asserted by tests, phase
  timings always emitted, and the first real run as the first datapoint. Explicitly not
  optimised: no concurrency at all, which closes the map's parallelism fog.
- [Assembly discovery under ambiguous output layouts](issues/14-assembly-discovery-under-ambiguous-output-layouts.md):
  **Reach does not predict layouts, it scans and verifies.** The ticket's candidate-path
  algorithm is dissolved rather than answered — enumerate `*.dll` excluding `obj/`, prune by
  expected assembly name before opening anything, then prove each survivor by its debug symbols
  and read its TFM from **`TargetFrameworkAttribute`** rather than a path segment, which is
  exactly what every ambiguous layout destroys on disk and metadata always had. Every listed
  case falls out for free, the empty outer-build directory included, because no path is ever
  computed. The payoff: **"assembly not found" becomes unambiguously an error** (exit 3) since
  the tree was searched, with projects outside analysis scope and projects producing no assembly
  excluded from the expected set. Two candidates for one instance **errors and names the
  disambiguating option**; newest-mtime guessing was rejected for converting a loud stop into
  silent under-selection. `-c`, `-o` and `--artifacts-path` demote from layout inputs to
  **narrowing hints**. Stale output is mostly free; the residue is a stale assembly under
  `--no-build` whose own source is unchanged but whose dependencies moved.
- [What counts as a changed member](issues/18-what-counts-as-a-changed-member.md): the unit of
  comparison is the **whole member declaration** with trivia stripped, not the body — free,
  since Roslyn already parses the file, and it closes `const` values, field initializers, enum
  members, attribute arguments and default parameter values in one move. It also settles the
  case ticket 07 escalated: **adding `[Fact]` to an existing method now registers as a change**,
  so PRD §4.2 rule 3 stays derivable from the change set and needs no separate mechanism. Type
  headers hash separately, giving whole-type widening when a test class gains a discoverability
  attribute. The consumer side is implemented rather than named —
  [ADR-0014](../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md):
  a **removed member or changed compile-time constant** widens every in-scope assembly
  transitively referencing the declaring one, because an inlined constant leaves no reference for
  the reverse walk to follow. Additions are self-covering, so the trigger stays narrow; not
  restricted to public surface, since `InternalsVisibleTo` defeats that. MVID stays out — it
  needs the baseline's binaries.
- [The unmappable-change rule table](issues/15-the-unmappable-change-rule-table.md): **six rows**,
  and **directory containment** is what stops the table degenerating into "select everything" — a
  `Directory.Build.props` widens projects at or below its own directory, which is the same rule
  MSBuild's props discovery uses, so it is not a heuristic. Only `global.json`, `nuget.config`,
  lock files and solution files are genuinely solution-wide. **No row adds dependents**: whole-assembly
  widening plus a backwards walk already reaches everything downstream. Row 6, the default, is the
  one deliberate under-selection — nearest ancestor project, or **nothing plus a notice** — taken
  because whole-solution selection would mean a README change runs the whole suite, and rows 1–5
  already enumerate every build-affecting root file. Generator detection stays conventional and
  **does not fall back to whole-suite**, which would fire hardest on solutions with no generator.
- [Fixture catalogue](issues/11-fixture-catalogue.md): **one fixture solution, four mandatory
  integration assertions, everything else in memory.** The four are the ones that fail *silently* —
  the NUnit `~` filter in both directions, an empty selection emitting **zero invocations**,
  whole-project fallback reporting `total: unknown`, and report determinism run twice. Determinism
  is what makes the rest cheap: with it pinned, **exact expected selections become the cheap
  option**, which settles the ticket's open question. Chunking demotes to a unit test of the
  chunker; layout tests relocate already-built output, now that discovery scans rather than
  predicts. Integration tests get their history from a **temp git repository per test**, which also
  makes ADR-0010's exit 4 testable. Negative assertions are named individually, including that a
  `Handler<Foo>` change *does* select `Handler<Bar>` tests, so ADR-0006's accepted cost stays a
  decision rather than drifting.
- [Project layout and ports](issues/10-project-layout-and-ports.md): three projects
  (`Reach.Cli`, `Reach.Core`, one test project), no `Reach.Contracts` until it has a consumer, and
  **one port** — `IProcessRunner`, covering `git` and `dotnet build`, which is two adapters at one
  seam. The ticket's most consequential call inverts its own framing: **the metadata reader is a
  parameter, not a port.** The core takes already-opened `PEReader`s, so an in-memory Roslyn
  compilation and a file on disk are the same type — the BCL type already *is* the seam, and
  wrapping it would be a shallow module that fails the deletion test. **No filesystem
  abstraction**: it is viral, and every filesystem test here needs a real git repository anyway.
  Nothing public in `Core`; the CLI is the only contract. Roslyn confined to one namespace by
  convention, with an architecture test named as the cheap upgrade. The deciding argument is the
  ticket's own: Reach will be pointed at its own repository, and an interface with one
  implementation forever is exactly what makes widening fan out.
- [Defining the over-selection measurement](issues/17-defining-the-over-selection-measurement.md):
  **a field in the report, not a harness** — every input is already carried for other reasons.
  The numerator is **tests that will run**, not tests selected, because the NUnit `~` rendering
  makes what runs a strict superset; both counts and the gap are reported. Unenumerable projects
  contribute to neither side and appear as a **second number** rather than a fake denominator,
  since counting them as zero would flatter the ratio in exactly the case where Reach runs a whole
  project. Unweighted by duration — Reach never runs tests and ADR-0001 forbids persisting, so
  there is no history to weight by. The widening delta uses the **cheap path-class count**, no
  second walk. And the stop condition is fixed *before* any number exists: over 70% mostly
  `widened` means stop, while over 70% mostly `compiled` means the codebase is too connected for
  any selector and is **not Reach's failure** — the row most likely to be misread as one.
- [The limitations register](issues/19-the-limitations-register.md): it **exists** —
  [docs/limitations.md](../../docs/limitations.md), twenty-three entries. One hand-written
  document, not generated in either direction, because the report says *this run hit this gap*
  while the register explains what the gap is and how it closes. What binds them is the code plus
  **one test: every `blind-spot` code must have an entry**, which turns ADR-0008's "named and
  surfaced" clause into a build failure and is the whole mechanism. That also answers
  detectability from the clean side — a detectable gap has a code and is mechanically tied to an
  entry; an undetectable one has no code, and the document is the only place it can live. Three
  directions rather than one: eleven under-selection, eight over-selection, two **measurement**
  (new from ticket 17), plus ADR-0010's environment entry. Notice suppression stays out, and if it
  ever ships `blind-spot` must be non-suppressible.

- [What documentation M1 ships](issues/20-what-documentation-m1-ships.md): **four documents, one
  of which already exists** — `README.md`, `docs/adopting-reach.md`, `docs/report-schema.md` and
  the limitations register — and **no new machinery**. Every obligation the resolved tickets had
  been banking lands in exactly one of them. The ticket settled a disagreement between two
  resolved tickets: ticket 07 assumed the register documents every notice code, ticket 19 scoped
  it to accepted *holes*, and both cannot hold once `no-changed-member-reached-a-test` is a code.
  The register keeps its identity; **the schema reference carries a complete code index**, full
  entry for codes that are not limitations, one line plus a link for codes that are — which also
  narrows ticket 07's "one page per notice code" to one index entry. **No JSON Schema file**: the
  contract is deliberately tolerant, so the instrument is a **committed example `report.json`
  that is the output of ticket 11's determinism fixture**, which means the test already written
  pins the documentation too. **Adoption cost is judged, not measured** — nothing rides on it the
  way ADR-0008's bargain rides on correctness — with one carve-out, since ADR-0010 makes exit 4
  the likeliest first run anyone has: the GitHub Actions recipe must be **the same YAML Reach's
  own CI runs**, which is a constraint [ticket 21](issues/21-ci-for-the-reach-repository.md)
  inherits. That verified recipe plus **a table row per provider** replaces the six recipes
  ticket 08 committed to — a deliberate, owner-taken narrowing, since ADR-0010 already is that
  table and an unverified recipe looks more authoritative than a row. The exit-code table lives
  with the pipeline author, not the schema; the `explain` verb's absence becomes a **"Reading a
  surprising selection"** section walking the committed example; and the README stops pointing
  adopters at the PRD as though a planning artifact were the manual.

## Not yet specified

The fog is clear. Everything in scope is now either decided above or a live ticket, and the
two remaining decisions both block the destination ticket,
[Write the spec](issues/12-write-the-spec.md):

- **What documentation M1 ships** graduated into
  [its own ticket](issues/20-what-documentation-m1-ships.md) and is now **resolved**. One piece
  had already graduated ahead of it and is likewise done —
  [the limitations register](issues/19-the-limitations-register.md) exists at
  [docs/limitations.md](../../docs/limitations.md) — because ADR-0008 made it load-bearing
  rather than documentation hygiene.
- **CI for the Reach repository itself** graduated into
  [its own ticket](issues/21-ci-for-the-reach-repository.md): what runs on a push, and whether
  the tool is published anywhere during M1.
- **How the spec is sliced into implementation tickets** was never separate work — it is what
  [Write the spec](issues/12-write-the-spec.md) does, and the seams it depends on were settled
  by [project layout and ports](issues/10-project-layout-and-ports.md).

## Out of scope

Scope is fixed by the destination: a spec for M1. These sit beyond it and do not graduate.

- **M0's build-to-test ratio measurement** (PRD §11, §12). A conscious override: gating a
  solo project on access to client solutions would stall it, and the ratio changes only
  whether the skeleton is worth building, which is already decided.
- **Framework models and the contracts assembly** (PRD §6) — M2.
- **Unmodelled-framework detection** (PRD §11) — M2.
- **Shadow mode** (PRD §8) — M2, and the instrument any narrowing would need.
- **Narrowing of any kind**, including mock-aware narrowing and PRD §11's container
  registrations. It conflicts with PRD §8's invariant and §6.2's additive-only model rule,
  and detecting a mock proves a mock exists, not that the real implementation is absent.
  Needs the conflict resolved and shadow mode to prove it safe.
- **Local mode** (PRD §7) — M3.
- **Previously-failed-test selection** — ruled out by ADR-0001; a pipeline concern.
- **MTP test-node UID emission** (`--filter-uid`, `TestNodeUidListFilter`), from
  [The report contract](issues/07-the-report-contract.md). It would dissolve the command-line
  length ceiling entirely for MTP hosts, but UIDs come from a discovery pass Reach does not
  run, and the in-process provider path is `[Experimental]` and needs a code change in the
  consumer's test project — the infrastructure ask PRD §9.1 exists to avoid. Revisit trigger:
  Reach running discovery for some other reason, or the filter provider stabilising.
- **An `explain` verb**, from [the CLI surface](issues/08-cli-surface.md). The version worth
  building reads `report.json` and renders one test's selection as prose, never re-analysing —
  PRD §12 requires a surprising selection to be explicable "without rerunning the tool", which
  makes a re-analysing `explain` a criterion violation dressed as a feature. But that same wording
  makes the *report* the thing that satisfies the criterion, leaving `explain` a convenience over a
  document already required to be sufficient. `--paths` ships in M1 so the data exists for whoever
  writes it. Revisit trigger: the first real surprising selection the report alone fails to explain.
- **A dry-run or preview verb**, from the same ticket. Not deferred but dissolved: `select` does not
  run tests, so a normal run already *is* the preview, and a second verb would create two code paths
  obliged to agree. The requirement it carried — an honest "this codebase over-selects too heavily
  to be worth adopting" — became the summary's over-selection percentage instead.
- **Notice suppression.** Deliberately absent from M1: a suppression switch un-surfaces
  exactly the holes ADR-0008's bargain depends on surfacing, and there is no evidence yet
  about which codes are noisy. If it ever ships, `blind-spot` must be non-suppressible.
