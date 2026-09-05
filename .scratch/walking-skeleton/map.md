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

## Not yet specified

- **What documentation M1 ships.** PRD §12 makes "a competent engineer can add Reach to an
  unfamiliar pipeline in under an hour, using only documentation" a success criterion, so
  documentation is in M1's scope, but nothing about its shape is decided.
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
