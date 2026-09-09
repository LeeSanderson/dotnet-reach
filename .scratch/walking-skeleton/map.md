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
- **The tool targets `net8.0` with `RollForward: LatestMajor`**, so it installs on any
  modern agent (PRD §1.2). *Under revision* — see
  [Tool target framework versus the Roslyn dependency](issues/13-tool-target-framework-versus-roslyn.md).
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

## Not yet specified

- **What documentation M1 ships.** PRD §12 makes "a competent engineer can add Reach to an
  unfamiliar pipeline in under an hour, using only documentation" a success criterion, so
  documentation is in M1's scope, but nothing about its shape is decided. Named obligations
  are already accumulating — the resolved tickets each flag the surprises they create — so
  this graduates once there is somewhere for them to land. One piece has already graduated:
  the limitations register is now
  [ticket 19](issues/19-the-limitations-register.md), because ADR-0008 made it load-bearing
  rather than documentation hygiene. The rest — install guide, CI recipes, the explanation
  of a surprising selection — is still fog, though
  [the report contract](issues/07-the-report-contract.md) has narrowed it: the docs now owe a
  schema reference with a compatibility promise, one page per notice code, the exit-code table,
  and the single line of pipeline guidance that falls out of it (*non-zero means run the whole
  suite*). What is still unpinned is the shape those take and who they are written for.
- **CI for the Reach repository itself** — build, test, pack, and whether the tool is
  published anywhere during M1.
- **Parallelism in graph construction**, and whether M1 commits to any concurrency at all.
- **How the spec is sliced into implementation tickets** — depends on which seams survive.

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
- **Notice suppression.** Deliberately absent from M1: a suppression switch un-surfaces
  exactly the holes ADR-0008's bargain depends on surfacing, and there is no evidence yet
  about which codes are noisy. If it ever ships, `blind-spot` must be non-suppressible.
