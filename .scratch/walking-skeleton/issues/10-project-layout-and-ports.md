# Project layout and ports

Type: grilling
Status: resolved
Blocked by: (none — 08, 09, 13 resolved)

## Question

How the repository is arranged, and where each seam lives.

**Projects.** `Reach.Cli` and `Reach.Core` at minimum. `Reach.Contracts` is M2 (PRD §6.2
is explicit that it must not be published until the built-in models have shaped it), so
does it exist at all yet? Where do tests live, and how many test projects — noting that
Reach's own repository is a solution Reach will eventually be pointed at, so its layout is
also a fixture.

**Ports.** Every environment boundary sits behind an abstraction so unit tests can mock
it: the filesystem (`System.IO.Abstractions`), git invocation, process execution for
`dotnet build`, and metadata and debug-symbol reads — the last one mattering most, because
it is what lets the core be tested against assemblies compiled in memory from source
strings rather than against files on disk.

**Seams.** These are named already and need homes: the join (changed declaration to graph
identity, deliberately abstract with two candidate implementations); assembly discovery;
test recognition, which should be data so that adding a framework touches no graph code;
filter rendering per dialect; framework and runner detection.

**Open questions:**

- Which seams are genuinely pluggable interfaces and which are just internal functions?
  Every interface added "for testability" that has exactly one implementation forever is a
  cost — and this codebase is being built by someone who will point Reach at it, where
  gratuitous interfaces are precisely what makes over-selection worse (PRD §11).
- Does the core depend on Roslyn at all, or only the change-detection component? PRD §9.2
  is emphatic that Roslyn is used to diff changed files and never to load a solution;
  keeping that boundary in the project structure makes the rule enforceable rather than
  aspirational.
- Public API surface: is anything in `Reach.Core` public in M1, or is the CLI the only
  contract?

Consult `codebase-design` alongside the usual skills for this one.

## Answer

Three projects, **one port**, nothing public. Resolved under
[ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md), using the
`codebase-design` vocabulary.

### Projects

```
Reach.Cli      → System.CommandLine 2.0.x; references Reach.Core
Reach.Core     → Microsoft.CodeAnalysis.CSharp 5.9.0, System.Reflection.Metadata
Reach.Tests    → one test project; InternalsVisibleTo from Core
```

No `Reach.Contracts` — PRD §6.2 forbids publishing it until the built-in models have shaped
it, and an unpublished contracts project is a shape nobody is pushing back on. It arrives in
M2 with its first consumer.

**One test project.** Splitting unit from integration tests buys a faster inner loop and costs
a second project plus a decision per test about where it lives; with
[ticket 11](11-fixture-catalogue.md)'s in-memory-first rule, the fast tests are already the
overwhelming majority and a filter on the four slow ones is cheaper than a project boundary.

[Ticket 13](13-tool-target-framework-versus-roslyn.md) settled the dependency list, and it is
worth keeping short: both packages resolve native `net10.0` assets with no facade chain, so
there is no version-conflict class to manage.

### Ports: one, not five

The ticket proposed four abstractions — filesystem, git, process execution, metadata and
debug-symbol reads. **One survives**, on the rule that one adapter means a hypothetical seam
and two adapters mean a real one.

**`IProcessRunner` is the only port.** It covers `git` and `dotnet build`, which is already two
adapters at one seam, and the fake is what makes ADR-0010's five CI environment variables, a
shallow clone and a failed build testable without any of those things being true. It is a deep
module: a small interface (run an argument vector, get exit code and streams) hiding process
lifetime, cancellation, stream draining and exit-code handling.

**The metadata reader is a parameter, not a port.** This is the ticket's most consequential
call, and it inverts its own framing — the ticket expected this seam to matter *most*, because
it is what lets the core be tested against assemblies compiled in memory. It does matter most,
and that is why it is not an interface: `Reach.Core` accepts already-opened
`MetadataReader`/`PEReader` instances, so an in-memory Roslyn compilation emitted to a
`MemoryStream` and a file on disk are *the same type*. There is nothing to fake, because the
BCL type already is the seam. An `IAssemblyReader` wrapping it would be a shallow module — an
interface nearly as complex as its implementation — and it would fail the deletion test:
delete it and no complexity reappears anywhere.

**No filesystem abstraction.** `System.IO.Abstractions` was considered and rejected. Real
temporary directories are less work to write than mock filesystem setup, and every filesystem
test in this design needs a real `git` repository anyway
([ticket 11](11-fixture-catalogue.md)), so the mock could not be used where the pressure is.
It is also viral: it changes every signature that touches a path, for a fake nothing needs.

The ticket's warning is the deciding argument and it deserves recording: **Reach will be
pointed at its own repository**, and an interface with one implementation forever is exactly
the indirection that makes widening fan out. Building the tool out of the pattern it punishes
would be a poor advertisement.

### Seams that are internal functions, not interfaces

Named as seams by the ticket, and each is a **module with an interface** in the
`codebase-design` sense — invariants, error modes and ordering — without being a C# `interface`:

- **The join** (changed declaration → `MethodId`). Stays abstract as a *decision*, but its two
  candidate implementations are chosen between by a spike and then one of them ships. One
  adapter, so no interface; the seam is a function signature, which is enough to swap behind.
- **Assembly discovery**, now scan-and-verify ([ticket 14](14-assembly-discovery-under-ambiguous-output-layouts.md)).
- **Filter rendering per dialect**, and **framework and runner detection**.
- **Test recognition is data**, per the ticket's own instinct — a table of framework, package,
  version range and attribute names, so adding a framework touches no graph code. This is the
  one place where the seam genuinely varies (three frameworks, four dialects), and data is a
  cheaper way to express that variation than four adapters.

### Roslyn's boundary, and the public surface

**Nothing in `Reach.Core` is public.** The CLI is the only contract, which is what makes
`report.json`'s schema version the compatibility promise
([ticket 07](07-the-report-contract.md)) rather than a type surface nobody meant to publish.
Tests reach in via `InternalsVisibleTo`.

Roslyn is confined to one namespace, `Reach.Core.Changes`, and PRD §9.2's rule — Roslyn diffs
changed files and never loads a solution — is enforced **by convention, not by a project split
or an architecture test**. Splitting it out would make the rule structural, but it buys a
project to enforce a rule with exactly one way to break it, on a codebase with one contributor.
Recorded as a deliberate omission so a reviewer does not read it as an oversight; an
architecture test is the cheap upgrade if a second contributor arrives.

### Consequences for other tickets

- **[Write the spec](12-write-the-spec.md)**: the project list, the single port, and the
  seams-as-functions list, so implementation tickets do not each invent an interface.
- **[Fixture catalogue](11-fixture-catalogue.md)**: consistent — one test project, in-memory
  first, temp git repositories for the four integration assertions.
