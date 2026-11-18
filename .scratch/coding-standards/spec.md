# Coding standards and a green CI

Status: ready-for-agent

Derived from a grilling session on 18 November 2026. Decisions that are surprising without
context are recorded in ADR-0016 (culture-invariant output), ADR-0017 (CRLF line endings) and
the Linux-ceiling paragraph added to ADR-0009.

## Problem Statement

CI on `main` has been red on both matrix legs for weeks, so nobody can tell whether a change
broke anything.

- **Windows** fails at `dotnet format --verify-no-changes`. The repository normalises text to
  LF through `.gitattributes`, but `.editorconfig` leaves `end_of_line` unset, so on Windows
  the formatter falls back to CRLF and objects to every line it re-wraps (argument lists split
  around a comment). Because the format step fails, the Windows test step never runs, so any
  Windows-only test failures are currently invisible.
- **Linux** passes formatting but fails 7 tests, from three independent causes:
  1. The human summary line formats its numbers with the current culture. On a `C` locale
     (the Ubuntu runner, most containers) it prints `50 %` instead of `50%`, and on other
     locales the digit grouping changes too. Real adopters see this.
  2. Source-binary correspondence uses raw PDB document paths. A path recorded with mixed
     separators (`src\deep/Widget.cs`) does not exist on Linux, so the document is classed as
     generated, nothing verifies, and the correspondence verdict silently becomes *Skipped*.
     That is a real Linux bug, and the one class of bug the CI matrix exists to catch.
  3. Four rendering/invocation tests size their "long selection" to overflow the Windows
     command-line ceiling (8,000) but not the Linux one (100,000), so on Linux the selection
     correctly stays inline and the tests' expectations of chunking or a response file fail.

Separately, the maintainer wants the codebase held to their published coding standard,
**Opinionated.DotNet.CodingStandards**, which today is not applied at all. Adding it
introduces roughly 800 analyzer diagnostics across 68 rules and a CRLF `end_of_line` default
that clashes with the current LF normalisation.

The fixture solution that the acceptance tests run Reach against is also outside the main
solution, so nothing checks that it builds, let alone against the standard.

## Solution

One branch, in this order: adopt the coding standard first, then fix the Linux failures, with
both CI legs green before merging.

- The package is added to every project through the shared build props, pinned to an exact
  version.
- The repository standardises on **CRLF**, matching the package, with shell scripts kept LF.
  Every file is renormalised once.
- Every diagnostic the package raises is fixed in code. Nothing is suppressed without the
  maintainer's explicit, per-rule approval; the only pre-approved suppression is the PDB
  checksum hashing (SHA1/MD5).
- The fixture solution's projects join the main solution, so the existing build and format
  gates cover them under the standard. The CI test step targets Reach's own test project only.
- The acceptance harness copies only git-tracked fixture files into its temporary repository,
  so in-place build output never leaks into an acceptance test.
- The summary line is culture-invariant; PDB document paths are normalised once, where they are
  read; the long-selection tests size their input from the platform's ceiling.

## User Stories

1. As the Reach maintainer, I want CI green on both Windows and Linux, so that a red build means
   something again.
2. As the Reach maintainer, I want the Windows test step to actually run, so that Windows-only
   test failures are not hidden behind a formatting failure.
3. As the Reach maintainer, I want my published coding standard applied to Reach, so that the
   tool I build follows the standard I recommend to others.
4. As the Reach maintainer, I want Reach to act as the first real consumer of the package, so
   that awkward package defaults are found here before adopters find them.
5. As the Reach maintainer, I want the package version pinned exactly, so that a package release
   can never turn `main` red without a commit in this repository.
6. As the Reach maintainer, I want to approve every suppression individually, so that the
   standard is not quietly weakened during adoption.
7. As the Reach maintainer, I want approved suppressions expressed as a `SuppressMessage`
   attribute at the specific site, with a justification, so that the rule keeps firing
   everywhere else.
8. As the Reach maintainer, I want the diagnostic clean-up landed one rule family per commit, so
   that each diff is reviewable on its own.
9. As a contributor on Windows, I want `dotnet format --verify-no-changes` to agree with my
   checkout, so that the pre-commit hook and CI never disagree about line endings.
10. As a contributor on Linux or macOS, I want shell scripts and Husky hooks checked out with LF,
    so that the pre-commit hook still runs after the switch to CRLF.
11. As a contributor, I want `.gitattributes` and `.editorconfig` to state the same line ending,
    so that git and the formatter cannot disagree on any platform.
12. As a future contributor, I want the CRLF decision recorded with its reasons, so that I do not
    "fix" it back to LF.
13. As an adopter running Reach in a container with a `C` locale, I want the summary line to read
    `selected 1 of 2 tests (50%)`, so that it matches the documentation and other machines.
14. As an adopter on a de-DE machine, I want the same digits as everyone else, so that the
    output I paste into a pull request reads the same to every reviewer.
15. As an adopter grepping CI logs, I want Reach's output to be identical across hosts, so that
    my log searches and run-to-run comparisons work.
16. As a future contributor adding an output line, I want a recorded rule that output is
    culture-invariant, so that I name the invariant culture when the analyzer makes me name one.
17. As an adopter on Linux whose PDBs record Windows or mixed separators, I want source-binary
    correspondence to verify my documents, so that Reach does not silently skip its safety check.
18. As the Reach maintainer, I want every PDB document path normalised in one place, so that no
    consumer can forget to normalise and reintroduce the bug.
19. As the Reach maintainer, I want the span join and the source-document list to share that one
    normalisation, so that "no raw PDB path escapes the reader" holds by construction.
20. As the Reach maintainer, I want the long-selection tests to overflow whatever ceiling the
    current platform has, so that they test chunking and response files on every OS.
21. As the Reach maintainer, I want the command-line ceiling to remain a fixed platform constant
    and not become a flag or seam, so that ADR-0009's "never a flag" holds.
22. As a future reader of ADR-0009, I want the Linux ceiling of 100,000 explained, so that the
    number is not mistaken for arbitrary.
23. As the Reach maintainer, I want the fixture solution in the main solution, so that the CI
    build proves it builds standalone and under the coding standard.
24. As the Reach maintainer, I want CI's test step to run only Reach's own tests, so that the
    fixture's VSTest-based and deliberately framework-less test projects do not break a
    testing-platform `dotnet test`.
25. As the Reach maintainer, I want the fixture's own tests exercised only through Reach in the
    acceptance tests, so that there is one end-to-end proof rather than two runners to maintain.
26. As the Reach maintainer, I want the acceptance harness to copy only git-tracked fixture
    files, so that Release output from the in-place CI build — built with the package and with
    embedded PDBs — never appears as extra assembly instances in an acceptance run.
27. As the Reach maintainer, I want any analyzer fix that rewrites a fixture line to change the
    acceptance test that matches that line in the same commit, so that the exact-text edits keep
    applying.
28. As the author of the acceptance-layer work, I want "every sample under the samples folder is
    in the main solution and builds clean under the package" recorded in that spec, so that
    samples not yet written are designed to meet it.
29. As the Reach maintainer, I want the SHA1 and MD5 checksum hashing in correspondence kept, with
    a justified suppression, so that PDBs hashed with older algorithms still verify.
30. As the Reach maintainer, I want package defects found during adoption listed for upstream, so
    that the package improves rather than every consumer carrying workarounds.
31. As an AFK agent implementing this, I want the order fixed (standard first, then Linux), so
    that I can attribute each new failure to the step that caused it.
32. As an AFK agent implementing this, I want to stop and ask before suppressing any rule, so that
    I do not violate the maintainer's approval rule.

## Implementation Decisions

**Order and delivery**
- Work happens on a branch with a pull request, so both CI legs run before merge. The coding
  standard lands first, then the Linux fixes. Each Linux failure group is its own commit.
- While the standard is being adopted, CI will be red for a mix of reasons. The 7 known Linux
  failures should be distinguished from anything the package's fixes introduce (for example by
  running the suite on Linux locally — WSL or a container — as fixes land).

**Package adoption**
- Opinionated.DotNet.CodingStandards is added to the shared build props as a package reference
  with all assets private, pinned to exact version **0.0.22**. No Central Package Management is
  introduced. Upgrades are deliberate commits that fix any new diagnostics in the same commit.
- Properties the shared props already set (e.g. `LangVersion=default`) keep winning, because the
  package only sets properties that are still empty.
- The package's automatic `InternalsVisibleTo("<Project>.Tests")` names projects that do not
  exist here. It is harmless and left alone.
- Diagnostics are fixed in code, one rule family per commit. `dotnet format` handles what it can
  automatically (naming, unused usings, whitespace); the rest is manual. Expected large groups:
  MA0211, CS1573, IDE1006 (underscore-prefixed fields), MA0181, S109, SA1649.
- **No suppression without the maintainer's explicit approval for that rule.** An approved
  suppression is normally a `SuppressMessage` attribute at the specific site with a
  justification. Whether a disagreement also becomes an upstream package change is the
  maintainer's call, case by case.
- **Pre-approved:** CA5350 and CA5351 on the SHA1 and MD5 branches of correspondence's document
  hashing. The justification is that the hash must match the algorithm the compiler recorded in
  the PDB; it is not used for security.

**Line endings (ADR-0017)**
- `.gitattributes` becomes `text=auto` with `eol=crlf` for everything, with explicit `eol=lf`
  exceptions for `*.sh` and everything under `.husky/`. The `.slnx` and YAML files follow the
  CRLF default.
- `.editorconfig` sets `end_of_line = crlf` under `[*]`, and its comment claiming the setting is
  "left unset on purpose" is rewritten.
- One commit renormalises the whole repository (`git add --renormalize`), on its own and with no
  other changes, so it can be skipped in blame.
- The package's `insert_final_newline = false` is overridden by the repository's
  `insert_final_newline = true`, which stays.

**Fixtures in the main solution**
- The fixture solution's five projects are added to the main solution under a `/samples/`
  solution folder, at their current location. When the acceptance-layer work moves samples, the
  rule follows them.
- Built in place, the fixtures inherit the root build props and therefore the package. Fixture
  code is fixed to pass. Where a fix rewrites a line that an acceptance test matches by exact
  text, the test changes in the same commit.
- The CI test step targets Reach's own test project explicitly instead of the solution. The
  fixture's test projects are compiled by the build step but never run by `dotnet test`. They run
  only through Reach, inside the acceptance tests.
- The acceptance harness's fixture copy enumerates **git-tracked files only** instead of every
  file on disk, so `bin/`, `obj/`, IDE folders and test results are never copied. The
  temporary-repository build stays exactly as plain as today; acceptance still covers the
  separate-PDB layout.
- The acceptance-layer spec gains the rule: every sample solution is in the main solution under
  `/samples/` and builds clean under the coding standard.

**Culture-invariant output (ADR-0016)**
- The measurement summary line is built with the invariant culture. The percentage is written as
  a rounded whole number followed by a literal `%`, not with the `P0` format specifier, because
  even the invariant percent pattern inserts a space. The line reads
  `selected 1,234 of 5,678 tests (50%)` on every host.
- Wherever the analyzers (CA1305, MA0076) flag culture-sensitive formatting elsewhere in output
  paths, the resolution is the invariant culture, not the current culture.

**PDB document paths normalised at the source**
- A single helper on the source-document module turns a raw PDB document name into a normalised
  path, given the metadata reader and the document handle.
- The source-document list uses it, so every source document's path is normalised when it is
  read. Correspondence (its de-duplication key, existence check, visibility check and hashing)
  therefore sees normalised paths without doing anything itself.
- The span join's sequence-point reading uses the same helper in place of its inline
  normalisation.
- The tier ladder's now-redundant normalisation of a source document's path is removed. Its
  normalisation of the working-tree side stays.

**Command-line ceiling tests**
- No production change. The long-selection inputs in the rendering and invocation tests are
  sized from the platform's command-line ceiling constant (for example ceiling divided by the
  per-name length, plus margin), so they overflow on every OS.
- ADR-0009 gains a paragraph explaining Linux's 100,000-character ceiling (already written).

## Testing Decisions

- A good test here asserts externally visible behaviour: the summary line's text, a
  correspondence verdict, a delivery mechanism and its argv, a span join's result. It does not
  assert which helper normalised a path or how a string was assembled.
- **The CI workflow is the top-level seam.** Package adoption, the line-ending change, the
  fixtures joining the solution and the git-tracked copy are all proven by both matrix legs
  passing restore, warnings-as-errors build, format verification and test. No new tests are
  written for these; the gate is the test.
- **The seven existing Linux failures are the seam for the Linux fixes:**
  - The measurement and human-summary tests prove culture invariance, **unchanged**. They are
    correct as written; the production code was wrong.
  - The correspondence test for a document recorded with mixed separators proves the PDB-path
    fix, **unchanged**.
  - The rendering chunker-partition test and the three invocation tests (response file, side-car
    overwrite, host with no channel) prove the ceiling fix, with only their input size changed.
- **One added case at an existing seam:** a span-join test over a PDB whose document is recorded
  with mixed separators, so the shared helper is covered from the span join's side. Prior art:
  the correspondence tests already build such a PDB through the in-memory compilation helper.
  Add it only if that helper can produce the shape.
- No dedicated test for the git-tracked fixture copy. It is test infrastructure, and the
  acceptance tests exercise it in CI directly after the in-place build, which is exactly the
  situation it guards against. A regression shows up as extra assembly instances in acceptance
  assertions.
- Prior art for assertion style: the existing reporting, correspondence, rendering, invocation
  and span-join test classes in Reach's test project.

## Out of Scope

- Moving the fixture to the samples folder, the Basics, Frameworks and Ledger samples, and the
  patch mechanism. These belong to the acceptance-layer work; this spec only adds the
  solution-membership rule to that spec.
- Running the fixture solution's own test projects directly in CI.
- Making the command-line ceiling injectable or configurable.
- Changes to the package itself. Upstream items are listed below and tracked separately.
- Introducing Central Package Management.
- Removing the package's automatic `InternalsVisibleTo`.
- Any suppression not explicitly approved by the maintainer.

## Further Notes

- **Upstream items for Opinionated.DotNet.CodingStandards**, found during this work:
  - `insert_final_newline = false` in the global defaults; consumers commonly want `true`.
  - The automatic `InternalsVisibleTo` assumes `<Project>.Tests`. It should be opt-in, or match
    the real test project's name.
  - The test-project config only disables GMNG01 and GMDTR12, which appear to be leftovers from
    another package and do nothing.
  - The README install sample shows version `0.0.2`, which looks like a typo.
- When the Windows test step runs for the first time in a while, it may surface Windows-only
  failures. Treat those as in scope: the goal is both legs green.
- Research numbers (804 diagnostics, 68 rules: Core 626, Tests 163, Cli 15) came from a trial
  build of a copy of the repository and will shift slightly once CRLF is applied.
- The dogfood workflow runs Reach on its own repository. With `DebugType=embedded` from the
  package, Reach's own PDBs become embedded. Reach supports embedded symbols (covered by the
  correspondence tests), but watch the dogfood run on the PR.
