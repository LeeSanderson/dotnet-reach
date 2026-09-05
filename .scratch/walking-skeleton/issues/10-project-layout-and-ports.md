# Project layout and ports

Type: grilling
Status: open
Blocked by: 08, 09

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
