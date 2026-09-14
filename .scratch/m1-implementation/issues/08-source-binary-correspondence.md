# Source–binary correspondence verification

Status: ready-for-agent
Depends on: 07
Spec: [§8](../../walking-skeleton/spec.md#8-sourcebinary-correspondence), [§9.6](../../walking-skeleton/spec.md#96-what-reach-cannot-observe) · [ADR-0003](../../../docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md)

## Goal

Prove that the assemblies Reach is about to read were compiled from the source it diffed —
**in both build modes** — and stop the run if they were not.

## Scope

> **Verify the mechanism before relying on the design. This is the ticket's first acceptance
> criterion, not a footnote.** ADR-0003's claim that portable PDBs record **per-document source
> checksums** is load-bearing and **has never been checked against real build output**. Before
> implementing anything, run a real `dotnet build` and confirm the checksums are present, which
> algorithm they use, and that recomputing them over the source file on disk reproduces them.
> **If the claim does not hold, stop and report it** — this section is redesigned, not worked
> around, and everything downstream of it changes.

**The check.** For each first-party assembly instance, enumerate the documents its portable
debug symbols record, and compare the recorded checksum against the file on disk. **A mismatch
is exit 5.**

**It runs in both modes, and that inverts what people expect.** PRD §5 originally concluded
freshness could not be established, because it considered only file timestamps — which git does
not preserve, so checking out an older commit onto a warm agent can leave source files *older*
than the binaries beside them and MSBuild will correctly conclude nothing needs rebuilding while
the binaries belong to a different commit. Source checksums are not a timestamp heuristic; they
are the compiler's own record of the bytes it compiled. **The mode that trusts the caller more
(`--no-build`) is the mode that detects more**, because default mode quietly recompiles the
changed file and every checksum then agrees. That asymmetry belongs in the documentation
(ticket 20) as well as here.

Consequences to implement:

- **Debug symbols must be present.** `DebugType` defaults to `portable` in Release as well as
  Debug, so this forces nobody into a Debug build. **`$(DebugSymbols)` is a false signal** — it
  evaluates `false` in Release while a PDB is still produced — so the property to read, if any
  is read at all, is `DebugType`. Handle both a `.pdb` beside the assembly and symbols embedded
  in it.
- **Source-generated documents have no file on disk to hash and need an explicit skip rule.**
- The document enumeration does **triple** duty and should be written once: it verifies
  correspondence, it classifies first-party assemblies (ticket 07), and it feeds tier 2's
  "which assemblies list this document" lookup (ticket 16).

**The untracked-and-ignored classification.** The same enumeration classifies every first-party
document three ways:

| Class | Meaning | Response |
|---|---|---|
| on disk and tracked | visible to change detection | normal |
| on disk, untracked **and** git-ignored | **invisible** — compiled source Reach cannot see changes to | `ignored-untracked-assembly` notice, **report only** |
| not on disk | compiler-generated | skip per the rule above |

**Report only, never select.** Selecting on the second would widen every project's assembly on
every run, because `obj/` lands in that category for every project — which is also why the check
suppresses documents under an `obj/` or `bin/` path segment. That path heuristic is an
approximation Reach is stuck with, since it does not run MSBuild and cannot read
`IntermediateOutputPath`; it can only ever cost a warning, never a test, so it is not
load-bearing on correctness.

Ignored *content* copied to output — a gitignored `appsettings.Development.json` — has no PDB
document and no cheap tell, so it stays purely documented.

**Path comparison is where Windows and Linux differ**, and a bug here is silent under-selection.
Normalise separators and decide case sensitivity deliberately rather than by accident; CI runs
both operating systems for exactly this reason.

## Acceptance criteria

1. **The mechanism is verified against real build output** — a test that builds a fixture
   project and asserts per-document checksums are present and reproducible from the file on
   disk. If they are not, this ticket stops and reports.
2. An integration test: build, then edit a source file without rebuilding, then run with
   `--no-build` → **exit 5**, naming the document.
3. The same edit in default mode → the build recompiles, checksums agree, run proceeds.
4. An assembly with embedded symbols verifies identically to one with a `.pdb` beside it.
5. A source-generated document does not fail the check.
6. A first-party document that is untracked and git-ignored emits `ignored-untracked-assembly`
   and does **not** enter the change set.
7. Documents under `obj/` and `bin/` emit no notice.
8. The verdict reaches the report envelope in all three states (verified, skipped, failed).
9. Path comparison passes on Windows and Linux, asserted with a mixed-separator fixture.

## Out of scope

Doing anything with the classification beyond reporting it. MVID comparison — registered as an
upgrade path, needs the baseline's binaries, out of scope for M1.
