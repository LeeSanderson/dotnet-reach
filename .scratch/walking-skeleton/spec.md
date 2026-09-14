# Reach M1 — the walking skeleton

**Status:** Specification · 14 September 2026
**Owner:** Lee Sanderson
**Supersedes nothing.** Derived from [PRD.md](../../PRD.md) v0.2, the fifteen records in
[docs/adr/](../../docs/adr/), and the twenty-two resolved decision tickets of
[the walking-skeleton map](map.md).

---

## 0. How to read this document

This is the buildable form of a design that was decided one question at a time. It draws
those decisions together so a fresh agent or engineer can build M1 **without rereading the
map**. Where a decision is surprising, the spec states the rule and links the record that
argues it; it does not re-argue it.

Three reading rules:

- **[CONTEXT.md](../../CONTEXT.md) is the vocabulary.** Every term in bold on first use is
  defined there. Identifiers and prose in the implementation should use those words and
  avoid the listed alternatives.
- **Direction is stated for every rule.** *Errs over* means the rule may run tests that did
  not need to run — wasteful, safe. *Errs under* means it may fail to run a test that would
  have caught a regression — a correctness bug, permitted in M1 only under §1.2. *Stops*
  means the run fails rather than answering.
- **Where the ADRs and this document disagree, the ADR wins**, and this document has a bug.

§§1–17 are the specification. §18 lists the implementation tickets and the order they land
in. §19 is the index from every claim back to the record that carries its reasoning.

---

## 1. The standing rules

Everything below is subordinate to these three, in this order.

### 1.1 The correctness rule

PRD §8's invariant: *an empty or reduced **selection** must never be reachable from missing
data.* Every failure widens the selection or stops the run. Nothing narrows it.

**Under-selection** is a correctness bug, not a tuning issue. Where a design question is
genuinely uncertain, take the option that widens.

### 1.2 M1's bounded exception

PRD §8.1 and [ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md): M1 ships
with known under-selection holes, each named in the **limitations register** and surfaced in
the **report**, rather than widening for every hole.

**The condition is load-bearing.** The exception holds only while every accepted hole is in
[docs/limitations.md](../../docs/limitations.md) and, where Reach can detect an instance,
carries a **notice** code. If the register lapses, the exception lapses and §1.1 governs
again. §14.3's parity test is what makes this mechanical rather than a promise.

### 1.3 Resolve at 80/20

[ADR-0012](../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md): where a
question has a simple answer covering the common case and a complete answer covering every
edge, M1 takes the simple one and writes the constraint down. A simplification that
over-selects is free. One that under-selects owes a register entry, and a notice code
wherever Reach can detect an instance.

This does **not** license leaving a question undecided. Each simplification below states the
rule adopted, the case it does not cover, and the direction that case fails in.

---

## 2. What M1 is

One command, `dotnet reach select`, which reads a solution's compiled output and a git
baseline and emits:

- **`report.json`** — the canonical, framework-agnostic record of the run. The product
  surface, with a versioned schema and a compatibility promise.
- **Complete argument vectors** per **assembly instance**, ready for a later pipeline step to
  execute.
- **A human summary** on stdout, explicitly not a contract.

M1 ships **no framework models** (PRD §12). Whole-project, whole-assembly and whole-type
widening are not models and are in scope. Local mode, shadow mode, narrowing of any kind, an
`explain` verb and a dry-run verb are out — see §17.

---

## 3. The pipeline

Nine phases, in order. Each is timed and every timing reaches the report envelope (§13.7).

| # | Phase | Output | Fails with |
|---|---|---|---|
| 1 | **Resolve the target** | one solution or one test project | exit 1 |
| 2 | **Resolve the baseline** | a SHA | exit 4 |
| 3 | **Resolve scope** | test projects, **analysis scope**, **project graph**, expected assembly-instance set | exit 1, exit 3 |
| 4 | **Build** (unless `--no-build`) | compiled output on disk | exit 2 |
| 5 | **Discover assemblies** | one file per expected assembly instance | exit 3 |
| 6 | **Verify correspondence** | a verdict per **first-party assembly** | exit 5 |
| 7 | **Compute the changed set** | changes, each routed to a tier | — |
| 8 | **Build the call graph** and walk it backwards | a selection per assembly instance | — |
| 9 | **Render and report** | argv vectors, `report.json`, summary | — |

Phases 1–6 establish that the question can be answered. Phases 7–9 answer it. **No phase
after 6 can fail the run**: from that point on, missing information widens (§1.1) and is
disclosed as a notice.

Phase 4 precedes phase 5 because discovery scans what the build produced. Phase 6 precedes
phase 7 so that a source–binary mismatch is caught before any analysis is attributed to it.
Phase 7 precedes phase 8 because the changed set determines nothing about graph
construction — the graph is built over the whole analysis scope regardless — but the roots
must exist before the walk.

---

## 4. The command line

```
dotnet reach select [<SOLUTION|PROJECT>] [options] [-- <dotnet build args>]
```

One verb, one positional, eleven options.

| Option | Default | Record |
|---|---|---|
| `<SOLUTION\|PROJECT>` positional | discovered in the working directory | [ADR-0011](../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md) |
| `--base <ref>` | auto-detected | [ADR-0010](../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md) |
| `-c\|--configuration <name>` | the SDK's default | §6.3 |
| `-o\|--output <dir>` | unset | §6.3 |
| `--artifacts-path <dir>` | unset | §6.3 |
| `--no-build` | off | §5 |
| `--report <path\|->` | `.reach/report.json` | §13.8 |
| `--report-dir <dir>` | `.reach` | §13.8 |
| `--paths` | off | §13.4 |
| `--list-unselected` | off | §13.5 |
| `--runsettings <path>` | unset | [ADR-0009](../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md) |
| `-v\|--verbosity <level>` | `normal` | §4.4 |
| `--no-color` | auto-detected | §4.4 |

### 4.1 The verb is required

`dotnet reach` with no arguments prints help and **exits 1**. Two reasons, both load-bearing:
the default mode invokes `dotnet build`, so the one invocation a newcomer is guaranteed to
try must not trigger a multi-minute build and write files into their repository; and a bare
invocation writes no report, so exiting 0 would hand a mis-wired pipeline "success" with a
consumer looping over an `invocations` array that does not exist.

`--help` asked for explicitly exits 0. Documentation leads with `dotnet reach select` from
day one, so nothing changes when `watch` arrives in M3.

### 4.2 Target discovery

Current directory **only**, never recursive. Recursion is where "confidently wrong" lives: a
monorepo with six solutions gets a coin flip, and the wrong solution silently produces a
wrong analysis scope.

| Found | Result | Direction |
|---|---|---|
| exactly one `.sln`/`.slnx` | that solution wins, whatever projects sit beside it | **over** |
| more than one solution | exit 1, naming what was found | stops |
| no solution, exactly one project | that project | — |
| no solution, more than one project | exit 1, naming what was found | stops |
| a `.slnf` solution filter | exit 1, naming the underlying `.sln` | stops |

**This deliberately diverges from MSBuild**, which compares base names and errors when they
differ. For a build, a solution and a project are two ways of naming work; for Reach they are
two different **analysis scopes**, so choosing the project is choosing the narrower scope —
a silent under-selection with a plausible-looking report. A `.slnf` is rejected for the same
reason: honouring a declared subset shrinks analysis scope by exactly the mechanism
[ADR-0002](../../docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md)
forbids.

**The boundary this draws, and it governs §6.3 too:** Reach mirrors `dotnet build`'s **option
spellings** exactly — same long names, same short forms, same semantics — so a pipeline
author transfers what they already know. That rule governs spelling. It does **not** govern
resolution semantics, where the correctness rule outranks the convention.

Two cases discovery must handle explicitly:

- **A `.csproj` declaring no tests.** Analysis scope is the union of test-project closures,
  so the union is empty. This is a mis-invocation, not `nothing-selected` — reporting "no
  tests affected" would be a lie a pipeline believes. **Exit 1**, naming the project.
- **A `.csproj` outside every solution.** Legal: the solution only ever supplied the expected
  project list. It costs the check that turns a missing assembly into an error, so Reach
  emits a `scope` notice and continues.

### 4.3 `--` forwards to the build, except what Reach owns

Everything after `--` is forwarded verbatim to `dotnet build`. Without this, every adopter
who builds with `-p:` properties, `--no-restore` or a binlog is pushed onto `--no-build`
permanently, making the documented default the minority path.

**Reach scans the forwarded tokens for `-o`, `--output`, `--artifacts-path` and `-c` and
refuses with exit 1**, saying they must be given to Reach directly. A forwarded `-o` changes
the output layout through a channel Reach never read. The envelope records the forwarded argv
so a surprising run stays reproducible.

### 4.4 Streams, and the directory Reach owns

Human summary to stdout, notices and logs to stderr, JSON to a file. `--report -` sends JSON
to stdout and moves the summary to stderr. `-v|--verbosity` mirrors the SDK's
`quiet|minimal|normal|detailed|diagnostic`. Colour is auto-detected; `--no-color` and
`NO_COLOR` are honoured.

`.reach/` under the **working directory**, not beside the solution, holds `report.json` and
every response file and testlist. Reach writes `.reach/.gitignore` containing `*` on first
use, so the first adopter to run it locally does not commit a report. **Reach never cleans
the directory up** — the runner reads those files in a *later* pipeline step, so deleting
them on exit would break the contract Reach just published. The lifecycle is the caller's,
and the documentation says so.

### 4.5 No option may narrow scope

No test-project include/exclude, no target-framework restriction, no `--no-untracked`. A
switch whose only possible effect is under-selection is a poor use of the option budget under
PRD §12's hour-to-adopt criterion. The chunking ceiling (§12.4) is detected per platform and
is never a flag: no adopter can reason about an 8,191-character limit, so a wrong value would
be a bug rather than a knob.

`--runsettings` is documented **by its effect** — "merge Reach's filter into this runsettings
file and write the result" — because the name is a foot-gun: someone will pass it expecting
Reach to *use* their settings for a test run Reach never performs.

---

## 5. Build handling

**Default mode** invokes `dotnet build` through the `IProcessRunner` port. On an already-built
tree this is a fast no-op — MSBuild's own up-to-date checking decides, and Reach implements no
up-to-date check of its own.

**`--no-build`** uses the output already on disk.

**If the build fails, Reach exits 2, selects nothing and runs nothing.** There is no degraded
mode: a failed build already fails the pipeline, so a clever selection over a broken tree has
no consumer.

**Correspondence is verified in both modes** — §8. The mode that trusts the caller more is the
mode that detects more, which inverts what people expect and belongs in the documentation as
well as here: an invisible source file that changed after the binaries were built fails the
checksum under `--no-build`, while default mode quietly recompiles it and every checksum
agrees.

---

## 6. Scope, discovery and layout

### 6.1 Analysis scope

**Analysis scope is the union of the transitive project closures of the solution's test
projects.** Code outside every test closure cannot execute in any test process, so a change to
it cannot affect a test.

Closures are derived from `ProjectReference` elements in the project files, **not** from
assembly references in metadata: the compiler omits references to assemblies whose types are
never named, so the metadata closure is narrower than the real one.

**Build scope may be narrowed; changed-side analysis scope may not.** Analysing only the
projects that transitively depend on the changed project is the forbidden narrowing — a caller
usually references the interface, not the implementation, so the caller's project is not a
dependent of the implementation's project and would be excluded despite being affected.

A pipeline pointing Reach at a single test project must still restore the whole solution's
output, or scope cannot be established and Reach errors rather than answering narrowly.

Solutions are read with `Microsoft.VisualStudio.SolutionPersistence`, which reads both `.sln`
and `.slnx` with no MSBuild dependency, and is what `dotnet sln`, `Microsoft.Build.dll` and
NuGet.Client all use — so Reach's reading of a solution agrees with the build's by
construction. Caveats to encode: `OpenAsync` is async-only; `Type` is empty for a plain
`.csproj`, so switch on `Extension`; the type GUID differs between `.sln` and `.slnx` for the
same project; `.sln` paths are neither canonicalised nor separator-normalised.

### 6.2 Assembly discovery: scan and verify

**Reach does not predict layouts. It scans and verifies.** Layout is undecidable from disk —
the same `OutputPath` produces different shapes depending on whether it was set in the project
file or forwarded as an MSBuild global property, and nothing on disk records which happened —
so predicting it means enumerating every way MSBuild can be told where to put output and hoping
the list is complete.

1. **Enumerate** `*.dll` under the target's directory tree — the solution's directory, or the
   project's for a single-project target — excluding `obj/`, `.git/`, `node_modules/`, `.vs/`
   and `packages/`.
2. **Prune by file name** against the expected assembly simple names, taken from the solution's
   project list. This runs before anything is opened and is what keeps a scan over a large
   repository affordable.
3. **Open** each survivor's metadata and debug symbols. **First-party iff the symbols point at
   source inside the working tree.** Symbols sitting *beside* an assembly prove nothing —
   `.pdb` is an `AllowedReferenceRelatedFileExtension`, so dependencies' symbols are copied
   into consuming projects' output — so classification must resolve the document paths recorded
   *inside* the symbols.
4. **Read the target framework from `TargetFrameworkAttribute`, and the platform from
   `TargetPlatformAttribute`**, on the assembly itself, never from a path segment. This is the
   payoff: the TFM is what every ambiguous layout destroys on disk.
5. **Match** to the expected set of `(project, target framework)` **assembly instances**.

Every ambiguous case falls out rather than needing a rule: `OutputPath` in the project file
versus as a global property, artifacts output's missing TFM segment for single-targeted
projects, relative `-o` absolutised against the CLI's working directory, and the multi-targeted
outer build's computed-but-empty directory — which cannot read as a missing assembly because no
path is ever computed.

**"Assembly not found" is unambiguously an error: exit 3.** The tree was searched, so absence is
absence. Two things are not missing assemblies and must not trip it:

- a project outside the analysis scope — not expected;
- a project producing no output assembly — generator and analyser projects, and projects
  referenced with `ReferenceOutputAssembly=false` — excluded from the expected set.

**Two or more candidates for the same assembly instance is also exit 3**, whose meaning widens
from "assembly missing" to "assembly discovery failed". Reach prints the candidate list and
names the option that disambiguates. Guessing was rejected: newest-mtime silently picks a stale
Release build over a fresh Debug one about as often as not, converting a loud stop into
under-selection with no notice.

**Declared-moniker caveat.** `TargetFrameworkAttribute` does **not** carry the declared
target-framework string. `net10.0`, `net10.0-windows` and `net10.0-windows7.0` stamp identical
values, and the last two are byte-identical. `TargetPlatformAttribute` separates a
platform-suffixed instance from a plain one — so discovery still resolves two distinct assembly
instances and the ambiguity error does not misfire — but it cannot reconstruct the declared
string. Only the command line needs it; see §12.3.

### 6.3 Layout options are narrowing hints

`-c|--configuration`, `-o|--output` and `--artifacts-path` are **filters applied to scan
results**, not layout inputs. All optional; all spelled exactly as `dotnet build` spells them
(`--artifacts-path` is hyphenated and has no short form on any `dotnet` command). Their only
job is to break the ambiguity above, which is why none is required for the common case.
`--artifacts-path` cascades to `--no-build`.

### 6.4 Stale output

| Case | Response | Direction |
|---|---|---|
| output for a project no longer in the solution | matches no expected instance, ignored | — |
| output for an abandoned target framework | its `TargetFrameworkAttribute` matches no expected instance, ignored | — |
| a stale assembly whose own source changed | caught by §8 — **exit 5** | stops |
| a stale assembly whose own source did not change but whose dependencies did | **register entry**, `--no-build` only | **under** |

The last cannot arise in default mode, which runs the build. Detecting it needs dependency
timestamp comparison, which M1 does not do.

---

## 7. The baseline

**One option, `--base <ref>`, always merge-base semantics:** the baseline is
`merge-base(HEAD, <ref>)`. The two-option design collapses, because `merge-base(HEAD, C) == C`
for any ancestor commit, and where the two genuinely differ — a diverged commit — merge-base is
the answer the caller wanted anyway.

When `--base` is absent, the ref is detected in a fixed order, first hit wins, and **every
resolution is disclosed** in the human summary and as an `environment` notice carrying the
resolved SHA.

| Order | Variable | Provider | Note |
|---|---|---|---|
| 1 | `GITHUB_BASE_REF` | GitHub Actions | bare branch name; `pull_request` / `pull_request_target` only |
| 2 | `SYSTEM_PULLREQUEST_TARGETBRANCH` | Azure DevOps | **format varies by repo provider** — `refs/heads/main` for Azure Repos, bare `main` for GitHub-hosted; strip the prefix |
| 3 | `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` | GitLab CI | merge-request pipelines only |
| 4 | `BITBUCKET_PR_DESTINATION_BRANCH` | Bitbucket Pipelines | PR-triggered builds only |
| 5 | `CHANGE_TARGET` | Jenkins multibranch | unset on ordinary branch jobs |
| 6 | exactly one of `origin/main` / `origin/master` present locally | — | local-developer convenience; never fires in CI |
| 7 | — | — | **exit 4** |

**TeamCity is deliberately absent**: `teamcity.pullRequest.target.branch` is a configuration
parameter and TeamCity passes only `env.`-prefixed parameters to the build process. Its
documentation shows `--base` explicitly.

**The source-branch traps.** Every provider also exposes a *source* branch variable, and
picking one is the single detection bug that under-selects rather than over-selects: a source
branch resolves at or ahead of `HEAD`, producing an empty change set and a green pipeline.
`GITHUB_HEAD_REF`, `System.PullRequest.SourceBranch`, `CI_MERGE_REQUEST_SOURCE_BRANCH_NAME`,
`BITBUCKET_BRANCH`, `CHANGE_BRANCH` and Azure's `Build.SourceBranch` (set on *every* build,
reading `refs/pull/1/merge` on a PR) are the traps. Each is a named negative test.

**There is no default-branch fallback**, and this is the first thing a future reader will
propose adding. `refs/remotes/origin/HEAD` does not exist after a CI checkout at any fetch
depth, because neither GitHub Actions nor the Azure Pipelines agent runs `git clone` — both do
`git init`, `git remote add`, `git fetch`, and only `clone` creates that ref. Recovering it
means a network call inside a step whose whole purpose is to be cheap and offline.

**Exit 4's message is a product surface.** `actions/checkout` defaults to `fetch-depth: 1` and
on a PR build fetches exactly one refspec, so there is no `origin/<target>` ref and no history
behind `HEAD`; `git merge-base` fails. **Reach does not work under default CI settings**, and
the fix is one line of YAML — which makes exit 4 the likeliest result of anyone's first
pipeline run. The message detects the provider from the environment and prints the literal
line to add: `fetch-depth: 0` for GitHub Actions, `fetchDepth: 0` for Azure DevOps.
"Baseline unresolvable" alone would convert a one-line fix into a support question.

**A baseline resolving to `HEAD` emits its own notice**, whether or not the change set turns
out empty. The resolved SHA leads the human summary and sits in the report envelope: the
empty-change-set trap *is* a baseline that quietly resolved to `HEAD`, and an audit trail that
cannot say what it audited will not catch it.

A shallow clone is exit 4, loudly, never a silently truncated history.

---

## 8. Source–binary correspondence

Reach compares the source files it diffed against the **per-document checksums recorded in the
portable debug symbols**, in **both** build modes. A mismatch is **exit 5**.

This is not a timestamp heuristic — timestamps are untrustworthy because git does not preserve
them, so checking out an older commit onto a warm agent can leave source older than the
binaries beside it and MSBuild will correctly conclude nothing needs rebuilding. Source
checksums are the compiler's own record of the bytes it compiled.

Consequences to implement:

- **Debug symbols must be present in the build output.** `DebugType` defaults to `portable` in
  Release as well as Debug, so this forces nobody into a Debug build. `$(DebugSymbols)` is a
  false signal — it evaluates `false` in Release while a PDB is still produced — so the
  property to read is `DebugType`.
- **Source-generated documents have no file on disk to hash** and need an explicit skip rule.
- The document enumeration this requires does double duty: it classifies first-party assemblies
  (§6.2) and detects ignored-and-untracked compiled files (§9.5).

> **Carried forward, and it is an acceptance criterion rather than an assumption.** The claim
> that portable PDBs record per-document source checksums is **load-bearing and has not been
> verified against real build output**. The implementation ticket for this phase verifies it
> against a real `dotnet build` before the design is relied on. If it does not hold, this
> section is redesigned, not worked around.

---

## 9. The changed set

### 9.1 Sources

Two commands, and between them they span everything:

```
git diff --no-renames --name-status <baseline>     # committed since baseline, staged, unstaged
git ls-files --others --exclude-standard           # untracked
```

**Untracked files are always included, with no flag.** Opt-in under-selects by default; a
`--no-untracked` escape hatch is a switch whose only possible effect is under-selection, and
the pathological case it would exist for belongs in the consumer's `.gitignore`.
`--exclude-standard` is load-bearing rather than tidiness: without it every generated `.cs`
under `obj/` returns as untracked and every project's assembly widens on every run. **Errs
over.** An untracked `.cs` file entering the change set is worth a report line — it is either
intentional codegen or a forgotten `git add`.

An untracked file has no baseline revision, so every type it declares is an added type and
every member a root. No special case needed.

### 9.2 The unit of comparison is the declaration

Reach parses changed files with Roslyn at **both** revisions — the working tree, and
`git show <baseline>:<path>` — and matches **declared types** keyed on **fully-qualified name
plus arity**. Paths are read from git for the path-based tiers; git is never asked to interpret
them.

**A member's hash covers its whole declaration with trivia stripped** — modifiers, attributes,
signature, parameter defaults, initializer and body. Not just the body. Roslyn is already
parsing the file, so this costs nothing and closes the declaring-side gap in one move: `const`
values, field initializers, enum members, attribute arguments and default parameter values all
sit inside the declaration.

It also settles PRD §4.2 rule 3: **adding `[Fact]` to an existing method registers as a
change**, so "new since the baseline" stays derivable from the change set and needs no separate
mechanism — there is no affordable one, and now none is needed.

**Type headers hash separately.** A type's modifiers, attributes, base list and type parameters
are their own unit, and a change to that header is **whole-type widening**. This covers a test
class gaining an attribute that makes its methods discoverable, which no member-level
comparison would see.

**Stripping trivia is what preserves "comment and formatting churn must not select."** That
property now has to hold over a larger syntactic surface, which makes it a fixture rather than
an assumption.

This matching is name-keyed, which only *sounds* like it contradicts PRD §9.4's "identity must
be integers derived from metadata tokens, never strings". It is the source side, over the
changed files only, and it happens before the **join**. The graph side stays integer-keyed.

### 9.3 Deletions, moves and renames

**Renames are never detected.** `--no-renames` is passed, so a move arrives as a delete plus an
add and the type is simply found on both sides. Git's similarity threshold and the pairing of a
delete with a specific add stop existing as concepts, which removes a tuning knob whose wrong
setting under-selects.

| Change | Rule | Direction |
|---|---|---|
| a deleted **method** | **whole-type widening** on its declaring type, unconditionally | over |
| a deleted **type** (file survives) | **whole-assembly widening** — no identity in the current binaries | over |
| a deleted **file** | routed as the deletion of every type it declared at baseline | over |
| a deleted file declaring nothing outside a type | additionally routed to tier 3 (§10) | — |
| a type moved between **namespaces** | delete plus add → whole-assembly widening | over |
| a type moved between **directories** only | found on both sides → whole-type widening | over |
| a renamed type | delete plus add; a rename that also moves members can leave a gap | **under**, registered |

**Why a deleted method widens unconditionally.** The tempting position — a deletion with a
first-party caller fails the build, one without a caller is inert — misses **rebinding**.
Delete `public override bool Equals(object? o)` from a class: nothing referenced it by name, so
compilation succeeds, and every test asserting two equal values are equal now compares
references. The same shape covers a deleted `override` falling back to the base implementation,
a deleted `operator ==` on a class falling back to reference equality, a deleted overload
re-binding an untouched call site, and a deleted partial method implementation whose calls the
compiler elides. Deletion did not remove a binding; it *rebound* one.

An **absorption test** that would prove most deletions inert is designed, registered and
deliberately **not adopted**: it is a proof in five clauses, each a place to be wrong in the
under-selecting direction. Unconditional whole-type widening has no clauses, and its cost is
smaller than it looks — a deletion almost always travels with edits to the same type, which
were going to select that type's tests anyway.

Whole-assembly widening still has to name an assembly for a document that appears in no PDB,
and does so by **directory containment**: the nearest ancestor project directory, which
reproduces the SDK's default `**/*.cs` globbing without running MSBuild. Errs over (a path
excluded by `<Compile Remove>` over-selects) and is imprecise only for linked files pulled in
from outside the project directory — an addition to the glob, so it only loses coverage the
ancestor rule was never going to have.

**The namespace/directory asymmetry will surprise someone**, so it belongs in the documentation
and not only here. A namespace is part of the metadata name, so the move changes what runtime
binding sees — serialization discriminators, convention-based registration, `Type.GetType` —
and those are blind spots Reach cannot see into.

### 9.4 Recompilation widening

**Trigger** — only these two:

- a **removed** member of any kind;
- a changed **compile-time constant**: a `const` value, an enum member value, or a default
  parameter value.

**Effect:** whole-assembly widening for every in-scope assembly instance that **transitively
references** the declaring assembly, through the **project graph**. Dozens of nodes, not
millions.

**Why the declaring side cannot cover it.** The compiler bakes compile-time constants into
consumers, so after inlining the consumer's source is byte-identical, its IL is different, and
its IL may no longer reference the declaring assembly *at all* — the inlined constant leaves no
trace of where it came from. Whole-assembly widening on the declaring assembly does not reach a
test that exercises the consumer.

**Additions trigger nothing**, and that is what makes a narrow trigger sufficient: an added
member is in the changed set and the re-bound call site's IL now points at it, so the reverse
walk finds it for free. A changed signature decomposes into a removal plus an addition, so the
removal half fires.

**Not narrowed to public surface** — `InternalsVisibleTo` and internal consts consumed by a
friend assembly are exactly the traps, and detecting the friend relationship costs more than the
widening saves. **Attribute arguments do not trigger it** — an attribute argument lands in the
declaring assembly's own metadata and is not inlined into consumers, so §9.2's declaration-level
hash already covers it.

**Errs over, with a large blast radius**, and the cost is accepted because it is **legible**:
the report's forward change list (§13.4) carries every change with the count of tests it
reached, so a pull request that selected the whole suite shows exactly which `const` did it. A
user can move the constant or accept the cost knowing why — both better than a quietly missing
test. It also makes the project graph load-bearing for **correctness**, not only for scope.

**MVID comparison stays out.** It is the honest general fix and it needs the baseline's
binaries; nothing in M1 builds a second revision and ADR-0001 rules out persisting them.
Registered as the upgrade path.

### 9.5 Parsing C# Reach does not know

The naive rule — *any parse error widens* — is not a safety net. **Roslyn checks language
versions at binding, not parsing**, and Reach only ever calls `ParseText`, so an older parser
meeting newer C# never throws:

| Outcome | Example | Reach sees it? |
|---|---|---|
| error diagnostic + local structural damage | a C# 14 extension block on an older Roslyn → `CS1513`/`CS1022`, skipped-tokens trivia | yes |
| error diagnostic, structure intact | `\e` escape → `CS1009` | yes |
| **clean parse, structurally wrong, zero diagnostics** | `record Person(string First)` on an older Roslyn parses as a *method* named `Person` | **no** |

**A clean parse never proves a correct parse.** Four rules:

1. **Parse with `LanguageVersion.Preview`, never `Latest`.** The highest-value line in this
   section, and it is one argument.
2. **An error diagnostic *or* `SkippedTokensTrivia` in a changed file triggers whole-assembly
   widening for its project, plus a `parse-failed` notice.** Skipped-tokens trivia earns its
   place because it is a structural signal rather than a diagnostic one. **Errs over.**
3. **A project whose declared `LangVersion` exceeds Reach's parser ceiling gets a
   `langversion-above-ceiling` notice, not widening.** With current Roslyn shipped the window
   is narrow, and widening on it would fire across whole modern codebases for a hazard that
   usually is not present.
4. **The silent misparse is a register entry** — direction under-selection, partially
   detectable, upgrade path *ship current Roslyn and exercise the parser against the newest SDK
   in CI*.

The hole is narrower than it looks, and the reason is worth keeping so nobody re-widens it: the
**same parser reads both revisions**, so a deterministic misparse still diffs stably, and a
phantom member simply fails the join and falls through to whole-assembly widening, which
over-selects. The genuine hole is a construct whose members are *swallowed* from the tree,
because a changed method inside one never enters the changed set at all.

### 9.6 What Reach cannot observe

`.gitignore` governs only *untracked* files, so the blind spot is precisely
**untracked-and-ignored** compiled source. In CI most of that population is derived rather than
authored, so a change to it has a visible cause; what remains genuinely invisible is a pipeline
step generating code from a source outside the repository.

**Detected and reported, never selected on.** §8's document enumeration classifies every
first-party PDB document three ways: on disk and tracked (visible), on disk but
untracked-and-ignored (**invisible**, `ignored-untracked-assembly`), or not on disk
(compiler-generated, §8's skip rule). Selecting on the second would widen every project's
assembly on every run, because `obj/` lands there for every project — which also means the
check suppresses documents under an `obj/` or `bin/` path segment, an approximation Reach is
stuck with because it does not run MSBuild and cannot read `IntermediateOutputPath`. That
heuristic can only ever cost a warning, never a test, so it is not load-bearing.

---

## 10. Routing an unmappable change: the tier ladder and the rule table

**Routing is per change, not per file, and the results union.** Per-file gating is the only
version that can return less than the sum of its parts, and the case it loses is the ordinary
refactoring commit: a deletion sharing a file with an edit, where the edit keeps the file on
tier 1 and the deletion resolves to nothing.

| Tier | Condition | Response |
|---|---|---|
| 1 | the change maps to a member | **join** it to a `MethodId` and walk (§11) |
| 2 | a changed file with no changed member, referenced by some assembly's debug symbols | **whole-assembly widening** on every assembly whose symbols list that document |
| 3 | no symbols reference the file | the rule table below |

### 10.1 The rule table

All widening here is **whole-assembly widening**, so the reverse walk runs normally afterwards.

| # | Change | Selects | Errs |
|---|---|---|---|
| 1 | `.csproj` | that project's assembly instances | over |
| 2 | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig` | every in-scope project **at or below that file's directory** | over |
| 3 | `global.json`, `nuget.config`, lock files, and any solution file | every in-scope project | over |
| 4 | a generator or analyser project (detected, §10.3) | every project that consumes it | over |
| 5 | content copied to output — `appsettings.json`, `.resx`, anything else | its containing project | over |
| 6 | **default: no other row matched** | nearest ancestor project, if any; otherwise **nothing**, with a notice | **under** |

**Directory containment is the load-bearing idea** and is what stops the table degenerating
into "select everything". A `Directory.Build.props` beside one service's projects widens that
service, not the solution — and since MSBuild's own props discovery walks *up* from each
project, containment is not a heuristic, it is the same rule the build uses. Only row 3 is
genuinely solution-wide, and those files are genuinely solution-wide in effect.

**No row adds dependents**, and this looks like an omission so it is worth stating: whole-assembly
widening puts every method of the assembly into the changed set, and reverse reachability then
walks *backwards*, so everything downstream is already reached. §9.4's recompilation widening is
the exception because inlining can *erase* the reference the reverse walk would have followed —
a different problem with a different mechanism.

### 10.2 Row 6 is a named under-selection

The only rule in Reach that deliberately errs toward selecting nothing. Taken anyway, as an owner
decision under §1.3.

The alternative is whole-solution selection for any unrecognised file, which means **a README
change runs the entire suite** — not conservatism with a cost, but a tool nobody keeps switched
on, firing on a large fraction of real pull requests. Rows 1–5 enumerate every build-affecting
file that lives at a repository root, so the residue genuinely is documentation-shaped.

**Fully detectable** — `unmapped-file-no-project` fires when row 6 matched with no ancestor
project, so the notice is precise rather than a blanket disclaimer. Upgrade path: a configurable
pattern list, deliberately not in M1 because there is no evidence yet about what real
repositories keep at their roots.

### 10.3 Generator detection is conventional, and says so when it fails

Reach checks the conventional markers — a `ProjectReference` carrying `OutputItemType="Analyzer"`,
and a project setting `IsRoslynComponent` or `EnforceExtendedAnalyzerRules` — and row 4 fires when
they match.

**When they do not match, Reach does not fall back to whole-suite selection.** A generator
arriving as a `PackageReference` or a bare `<Analyzer>` item is invisible, and the honest answer
is a register entry rather than a widening that fires hardest on codebases with no generator at
all. **Errs under**, undetectable, registered.

**A generator edge is not a call edge.** It is a compilation-input relation, so it does not enter
the call graph and gets no edge provenance; it is consulted by the rule table at the point a change
is routed. This keeps the seven edge kinds intact rather than growing an eighth that means
something categorically different.

---

## 11. The call graph

### 11.1 A node is an IL method definition

One `MethodDefinition`, per **assembly instance**. Never a source declaration, never a generic
instantiation.

- **Generics collapse.** One node per generic *definition*; the type arguments at a call site are
  not part of identity. Cost accepted: a change reachable only through `Handler<Foo>` also selects
  tests using only `Handler<Bar>`. **Errs over**, registered, and pinned by a named fixture so it
  stays a decision rather than drifting. The type argument remains *readable* at the call site
  (`MethodSpec` carries it), so a later framework model can use it without reversing this.
- **Accessors are nodes.** `get_Total`, `set_Total`, `op_Equality`, `add_Changed` are ordinary
  methods in IL. Folding them into a synthetic property node would make the graph invent a member
  IL does not have and reintroduce string-ish identity. The fan-out from a changed property,
  indexer, event or operator *declaration* to its accessors belongs in the **join**; the report
  renders the developer-facing name so nothing is lost. Two riders: an auto-property whose
  *initializer* changes is a change to the constructor, not to an accessor; a field-like event's
  generated `add`/`remove` fan out the same way.
- **Dispatch declarations are nodes.** `callvirt IRepo.Save` edges to a node for `IRepo.Save`, and
  widening edges run from *that* node to the implementations. Call sites edging straight to every
  implementation would smear widening across the graph where it can be neither isolated nor
  counted, which would break the measurement §15 depends on.

### 11.2 Method identity

```
bit 63        : discriminator
bits 62..32   : assembly-instance ordinal (31 bits)
bits 31..0    : metadata token, or interned external-reference id (32 bits)
```

One `readonly record struct MethodId(ulong Value)`. No strings, per PRD §9.4. A metadata token is
1 byte of table id plus 3 bytes of row index, so 32 bits is exact; 31 bits of ordinal is four
orders of magnitude more assemblies than any solution has.

**The target framework needs no field.** An assembly instance *is* one `(project, target
framework)` pair and each gets its own ordinal, so multi-targeting falls out of the ordinal and
the hottest struct in the tool stays 8 bytes. This is the single simplification that makes the
representation fit.

**Ordinal assignment.** First-party assembly instances are numbered from 0 in a deterministic
order — assembly simple name, then TFM moniker — from the solution's project list. External
assemblies are numbered above them in first-encounter order during a pass that itself runs in
sorted order. **No ordinal ever reaches the report** (§13 fixes the report on names), so report
determinism does not depend on ordinal stability; ordinals are a debugging convenience.

**The discriminator exists because widening needs anchors Reach never reads.** Roslyn emits
`callvirt` against the slot-defining declaration, which is routinely `object::ToString` or
`System.IDisposable::Dispose`, and Reach does not read those assemblies. Bit 63 set means the low
32 bits are an **interned external member reference**, keyed on assembly name plus canonical
signature. External nodes are widening anchors only: never roots, never walked into, no body ever
read. The interned set is bounded by the slots first-party types actually implement or override,
not by the BCL's size.

### 11.3 Cross-assembly resolution

Per target assembly instance, **one dictionary from a canonical signature string to `MethodId`**,
built once at load in O(methods), then one lookup per call site — O(call sites), not the
accidentally-quadratic shape PRD §9.4 warns about. §9.4 forbids strings in *identity*, not in a
build-once lookup.

Signatures are decoded through a minimal `ISignatureTypeProvider` emitting type names. It removes
the whole overload-ambiguity class a coarser key would leave behind.

**Resolution failure has three different right answers:**

| Case | Response | Direction |
|---|---|---|
| target assembly outside the analysis scope | normal and silent; intern an external anchor if a first-party type implements the slot, else nothing | **under**, registered |
| target assembly is first-party but the member does not resolve | **notice**, run continues — a build problem, not an analysis one; §8 is what should have caught it | — |
| residual ambiguity — varargs, `modopt`/`modreq`, function-pointer parameters | **edge to every candidate**, `signature-ambiguous` notice | over |

### 11.4 The seven edge kinds

| Edge | Provenance | Class | Narrowable? |
|---|---|---|---|
| `call`/`callvirt` to an exactly-resolved target | compiled call | **compiled** | never |
| `ldftn`/`ldvirtftn` capture | compiled capture | **compiled** | never |
| kernel method → compiler-generated member | containment | **synthesised** | never |
| initialization trigger → `.cctor` | type initialization | **synthesised** | never |
| widening through an interface | widened | **widened** | yes |
| widening through a virtual override | widened | **widened** | yes |
| `ldvirtftn` fan-out to overrides | widened | **widened** | yes |

**Three classes, not two.** Containment and type initialization are invented by Reach, but the
control flow they describe is not in doubt, so they are as non-removable as compiled edges.
**Widened** is the only speculative class and therefore the only class any future narrowing may
touch.

Edges accumulate during the metadata pass as a flat growable array of
`(from: MethodId, to: MethodId, provenance: byte)`. After the pass, sort by `to` once and build a
**CSR reverse adjacency** — an offsets array plus a targets array. The reverse index is inverted
*afterwards*, not maintained during the pass: the node count is not known until the pass ends, and
inverting once is cheaper than maintaining per-node lists while appending.

**Only the reverse index is built.** Reverse reachability walks backwards; the forward change list
carries counts derived from the walk's roots; opt-in hop-by-hop paths reconstruct backwards. A
forward index would be a second structure obliged to agree with the first.

### 11.5 Containment, and why it is not optional

`async Task<int> Aw()` reaches its own body only through
`AsyncTaskMethodBuilder<int>::Start<'<Aw>d__0'>` — `MoveNext` is invoked from inside the BCL. So a
walk backwards from a change inside *any* `async` method body dead-ends in an assembly that is not
first-party and is not analysed. **That is under-selection on ordinary modern C#, not an edge
case.**

The fix is exact rather than heuristic. `[AsyncStateMachine(typeof('<Aw>d__0'))]` and
`[IteratorStateMachineAttribute(typeof('<It>d__1'))]` name the generated type directly, so a
**containment edge** runs from the **kernel method** to the generated members. The name-mangling
convention (`<Foo>b__0`, `<Foo>g__Local|0_1`, `<>c__DisplayClass`) is the fallback for closures the
attributes do not cover.

Lambdas need no special rule — a lambda emits `ldftn '<>c'::'<Lam>b__2_0'` inside the kernel
method, so §11.6's capture rule covers it. Local functions are an ordinary `call`. Iterators do
`newobj '<It>d__1'::.ctor`, a real compiled edge, but `MoveNext` still arrives via interface
dispatch from the consumer's `foreach`, so containment carries it either way.

**Type initializers are their own edge kind.** An edge is synthesised from every method that
triggers initialization — `newobj`, static field or method access on the type — to that type's
`.cctor`. Cheap, because the trigger instructions are already being decoded, and it closes a real
hole: a `static readonly Func<int,int>` field compiles to `ldftn` **inside `.cctor`**, which
nothing visibly calls, so without this edge the captured lambda is orphaned.

### 11.6 The address-taken rule

`ldftn`/`ldvirtftn` name their target in metadata; `Invoke` and `calli` name nothing.

**An edge runs from the capturing method to the target at `ldftn`. `Invoke` and `calli` create no
edges.** `ldvirtftn` names a virtual method, so it also produces the widened edges to the
overrides. `delegate*` values fall out for free — they are `ldftn` like any other capture.

Edging every `Invoke` on a delegate type to every method ever captured into that type *looks* like
the widen-when-uncertain rule and is not conservatism: `Func<T>` and `Action` are structural types
shared by unrelated code, so it connects everything to everything and makes the graph useless
rather than safe. The capture-site rule is far less lossy than it looks, because a test that can
execute the target must have executed the code that created the delegate.

**The residue** — a pointer obtained without an `ldftn` Reach can see (`Delegate.CreateDelegate`,
`Marshal.GetFunctionPointerForDelegate`, native interop) — is the one place the correctness rule
does *not* force widening, because every method whose address is taken in analysed code already has
its capture-site edge, so the only widening available is unbounded. **Errs under**, partially
detectable (`calli-unresolved`), registered.

### 11.7 Widening

**Widening is bounded by the inferred receiver type** — the static type a dispatch site can be
shown to hold — recovered by a single-pass abstract interpretation of the evaluation stack tracking
static types only, inside the pass already decoding call instructions.

Resolution ladder, in order:

1. a `newobj` / `castclass` / `isinst` / `unbox.any` operand — an **exact** type
2. an `ldarg` / `ldloc` / `ldsfld` / `ldfld` signature type
3. the `callvirt` token itself
4. the slot-defining declaration

**Why the token alone is not enough.** Roslyn emits `callvirt` against the **slot-defining**
declaration, not the most-derived statically-known type: `x.ToString()` where `x` is statically a
type that declares an override still emits `callvirt System.Object::ToString()`, and
`using var r = new Res()` on a **sealed** class implementing `IDisposable` emits
`callvirt System.IDisposable::Dispose()` rather than a direct call. Taking the token at face value
makes fan-out maximal — every `ToString()` call site edges to one `object::ToString` node, which
widens to every override anywhere. That is not conservatism, it is a graph with no information in
it.

Rung 3 is stronger than it looks: `Constrained<T>(T x) where T : IShape` emits
`constrained. !!T; callvirt IShape::Area()`, so Roslyn puts the generic constraint in the token and
inference gets it without reading the constraint table.

**This is not narrowing.** A variable of static type `Sq` cannot hold a non-`Sq` in verifiable IL,
so the bound comes from the type system rather than from inference about runtime behaviour, and no
edge that could run is removed.

Mapping and residues:

- **`MethodImpl` is authoritative** for interface member → implementation, because an explicit
  interface implementation is deliberately not name-matchable. Name and signature matching is the
  fallback for implicit implementations.
- A **default interface method** is a real body on the interface, so widening from an interface
  node reaches the DIM body *alongside* every override.
- **Static abstract interface members widen to every implementer.** The implementation is selected
  by the generic instantiation at the call site — exactly what §11.1 discards — so nothing narrower
  is available. **Errs over**, registered, and the most visible cost of collapsing instantiations.
- A **stack merge** (a ternary compiling to branches pushing different types) takes the common
  supertype, so that one site keeps full fan-out. **Errs over.**
- An **unconstrained generic receiver** emits `constrained. !!T` with the token at
  `object::ToString`, and identity erases `T`, so it falls to rung 4. **Errs over.**
- **An interface-typed receiver infers to the interface** — correct and useless. The DI shape *is*
  an interface-typed receiver, so **PRD §11's main technical risk is untouched by receiver-type
  inference** and remains unmeasured until §15's number exists.

### 11.8 The join — deliberately abstract

> **Carried forward, and the spec must not pre-empt it.** Turning a changed declaration in source
> into a `MethodId` is a **seam**. Two candidate mechanisms are live — **signature keys** and **PDB
> line spans** — and the choice is made by a **spike at implementation time** that measures both
> against a fixture and picks one. This document fixes the identity a join must *produce* (§11.2)
> and what it must fan out (§11.1's accessors), not how a declaration reaches it.

What the join owes, whichever mechanism wins:

- a changed property, indexer, event or operator declaration fans out to its **accessors**;
- an auto-property whose initializer changed maps to the **constructor**;
- a declaration that fails to join **falls through to whole-assembly widening** for its project —
  over-selection, which is why a phantom member from a misparse (§9.5) is survivable;
- the report's `display` form (§13.6) is derivable from *either* candidate, so the report does not
  presuppose the outcome.

---

## 12. Selection, rendering and delivery

### 12.1 The four selection rules

A test method is selected if any hold:

1. **`reverse-reachable`** — reachable backwards along call edges from a change.
2. **`own-source-changed`** — its own declaration changed.
3. **`new-since-baseline`** — derived from the change set (§9.2), not by comparing two test lists;
   Reach reads only the current compiled output and cannot enumerate the baseline's tests without
   building it.
4. **whole-project selection** — it lives in a project Reach has declared unanalysable. Deliberately
   **not** a member of the per-test `rules` array: the entry's `mode: run-all` already says it.

The rules are not interchangeable and only the first is an analysis result, so each selected test
records **which** rules selected it. A test selected only by rule 3 has no root and no path class,
so an empty roots list is valid *exactly when* `reverse-reachable` is absent — which makes a bug
distinguishable from a fact.

**Selection granularity is the test method.** Individual cases of a parameterised test are never
selected independently. A method-level filter does match every case of a parameterised test in all
three supported frameworks, confirmed from adapter source. Counter-intuitively, `DisplayName=`
matches **nothing** for a theory.

### 12.2 Test recognition is data

A table of framework, package, version range and attribute names, so adding a framework touches no
graph code. M1 covers **xUnit (v2 and v3), NUnit and MSTest**; an unrecognised framework falls back
to **whole-project selection** with a `whole-project-fallback` notice and a test total of
**`unknown`**, never `0`.

**There is no xUnit v4.** The NuGet package `xunit.v3` is at *version* 4.0.0; "v3" is the
generation. That release changed the filter surface — it added an MTP `--filter` accepting VSTest
syntax, added `-displayName`, and dropped MTP v1 — so the **dialect** is gated on **package
version** as well as framework and runner host.

### 12.3 Rendering

**M1 renders for `dotnet test` only**, and this is not a gap left open — it is what keeps §12.6's
exit-8 claim honest.

The **runner host** is not a property of a test project: the same assembly answers to `dotnet test`,
to its own executable, and for some package versions to VSTest. Reach emits an argv, so **the argv
already is the choice of host**, and `dotnet test` is the one choice determinable from the assembly
— the generated entry point dispatches on `--internal-msbuild-node`, which `dotnet test` always
passes, so it is MTP regardless of `UseMicrosoftTestingPlatformRunner`.

Rendering the native dialect as well would double the matrix and manufacture the one genuinely
silent cross-host failure: the two hosts' option names intersect in exactly four — `culture`,
`debug`, `explicit`, `filter` — and MTP normalises a single dash to a double, so `-filter` is
accepted by **both hosts in different filter languages with no diagnostic either side**.

Three rules the renderer must implement:

- **NUnit renders with `~` (contains), never equality.** NUnit's VSTest `FullyQualifiedName`
  *includes* a parameterised test's arguments, and method-level equality matching survives only via
  an adapter re-parse that `UseNUnitFilter=false`, `DiscoveryMethod.Legacy` and the IDE path all
  disable. Reach cannot see a consumer's `.runsettings` and **cannot detect the failure**, so this
  follows widen-when-uncertain. **Errs over**, `nunit-filter-overmatch`, and §13.3 carries both
  counts.
- **Every invocation for a project with more than one assembly instance carries `-f <moniker>`.**
  `dotnet test` runs every target framework of a project, so a filter naming a test that exists
  under only one of them makes the others exit 8 — Reach would render the *correct* answer and fail
  the build with it. A single-instance project has nothing to cross-contaminate and gets no
  selector.
  - Moniker derivation is **metadata-only**: `.NETCoreApp,Version=vN.M` → `netN.M`,
    `.NETStandard,Version=v2.0` → `netstandard2.0`; **no `TargetPlatformAttribute` → no suffix and
    the moniker is exact**.
  - `TargetPlatformAttribute` **present** on a multi-instance project → the declared moniker is
    unrecoverable (it always carries a version, so `net10.0-windows` reads back as
    `net10.0-windows7.0`, which `-f` rejects, and the mapping is not injective). Reach emits one
    project-wide `run-all` invocation with no selector and reports
    `framework-selector-underivable`. **Errs over**, registered, with
    `dotnet msbuild -getProperty:TargetFrameworks` as the upgrade path.
- **An emitted `dotnet test` argv must never carry `-o`.** MTP-mode `dotnet test` has no `-o` at
  all and spells `--output <Minimal|Normal|Detailed>` to mean *test output verbosity*, while
  VSTest-mode `dotnet test` and `dotnet build` use `-o|--output` for a directory.

Argv arrays, **never shell strings**: there are already three escaping layers — filter grammar,
MSBuild, XML — and a shell would be a fourth nobody should own.

### 12.4 Delivery: private channels, and one degraded host

**The length ceiling is the normal path, not an edge case.** A `FullyQualifiedName=…` clause runs
60–90 characters, so `cmd`'s 8,191 limit arrives at roughly **100 selected test methods** and
`CreateProcessW`'s 32,767 at roughly **400**. An ordinary pull request on a large solution clears
both.

| Host | Channel |
|---|---|
| Microsoft.Testing.Platform | `@file.rsp` |
| xUnit v3 native CLI | `@@ file` |
| nunit-console | `--testlist=FILE` |
| **VSTest under `dotnet test`** | **none — chunk across multiple invocations** |

All three channels are **Reach's own files, passed as arguments**: they occupy no channel the
consumer configures, cannot merge with consumer state, cannot be silently intersected, and carry no
length limit worth budgeting for.

**`.runsettings` `<TestCaseFilter>` is not used by default despite having no length limit at all.**
Two independent disqualifiers: a pre-existing `<TestCaseFilter>` is **AND-ed** with Reach's, and a
consumer's filter is typically an exclusion, so the intersection is strictly smaller than the
selection — a **silent under-selection**; and only one settings file can be passed, so a file Reach
writes *replaces* the consumer's rather than merging. Detection cannot rescue either, because Reach
is invoked as a separate step that never sees the runner's arguments.

**`--runsettings <path>` is the explicit opt-in**: the caller has supplied the fact Reach could not
discover, so Reach may merge and emit a combined file. If that file already carries a
`<TestCaseFilter>`, Reach **refuses to render a filter for that project and downgrades it to
`run-all`** with a `runsettings-filter-conflict` notice. **Errs over**, registered.

`vstest.console.exe` invoked directly *does* accept `@file`, and is rejected: locating it means
discovering a Visual Studio installation or a test-platform package layout, a larger and less
reliable problem than the one it solves.

**Side-car files** live in the Reach-owned directory (§4.4), with deterministic names from project +
TFM + chunk index so a re-run overwrites rather than accumulates, and **absolute paths in the argv**
so a pipeline that changes directory between steps still works. **Never** a path MSBuild or Visual
Studio reads by convention — no `Directory.Build.props`, no project-root `.runsettings` — and never
the system temp directory, which agents clear between steps and which is the least inspectable place
to look when a filter misbehaves. Every chunk and every runsettings downgrade emits a notice.

### 12.5 An empty selection emits nothing

**A project with nothing selected gets `mode: skip` and an empty `invocations` array.** Not an empty
filter string, which runs everything. `--ignore-exit-code 8` — the usual way to stop a zero-match
run failing a build — **stopped working on the .NET 11 SDK**, where zero-match handling moved to a
run-level verdict, so emitting no command at all is the only host- and SDK-version-independent
answer. Making `invocations` an array makes it the only **representable** answer rather than a rule
to remember: a consumer iterating blindly does nothing.

**A consumer's loop over `invocations` must start no process at all** for an empty selection. That is
an acceptance criterion (§16.2), driven and observed, not inferred.

**`run-all` with zero invocations is also representable**, from §12.3's underivable-moniker case:
the single project-wide invocation is carried by the **first entry in the report's own sort order**
and the rest carry empty arrays, so a consumer's loop issues exactly one command. **A consumer
reading one entry in isolation must therefore not infer "nothing to run" from an empty array alone —
`mode` is the field that says it.**

### 12.6 Exit 8 is an instrument, not a hazard

With §12.3's selector in place the enumeration is exhaustive, and **no path through Reach's design
renders a filter matching nothing**: an empty selection renders nothing; a chunk always carries at
least one test by construction; a multi-targeted project pins its framework; a consumer who wires one
filter solution-wide is outside Reach's design and **loud** (the run fails with `error: 1` and the
per-module line names the offending assembly); and a consumer who mixes hosts is unreachable because
M1 renders only `dotnet test`.

The claim *is* the mitigation, so it is stated rather than implied. It follows that **exit 8 from a
Reach invocation means the rendering matched nothing while the report claimed tests were selected —
a Reach bug and nothing else.** That goes in the exit-code section of the adoption guide.

**Reach never emits `--ignore-exit-code 8` and the documentation never recommends it.** It does not
merely suppress the signal, it erases the evidence: the zero-match module's line changes from *Zero
tests ran* to *passed* and the annotation disappears from the summary entirely.

Two residual ways to see exit 8 from a correct invocation are the consumer's own choice:
`--zero-tests-policy strict` when every selected test in a project is skipped, and a rebuild between
Reach's run and the test step.

---

## 13. The report

One JSON document per run. **The schema, not this section, is the contract**, and
`docs/report-schema.md` is its reference; what follows is the normative shape the implementation
must produce.

### 13.1 Shape

```jsonc
{
  "schemaVersion": 1,
  "outcome": "selected" | "nothing-selected" | "no-changes" | "failed",
  "reasons": ["no-changed-member-reached-a-test"],   // notice codes; required non-empty when nothing-selected
  "envelope": { /* §13.7 */ },
  "scope": {
    "testProjects": ["tests/Acme.Orders.Tests/Acme.Orders.Tests.csproj"],
    "assemblies": [ { "name": "Acme.Orders", "targetFramework": "net10.0" } ]
  },
  "summary": { /* §15 */ },
  "changes": [ /* §13.4 — forward-indexed, counts only */ ],
  "entries":  [ /* §13.2 — one per (test project, target framework) */ ],
  "notices":  [ /* §14 — one global array */ ]
}
```

### 13.2 Entries are keyed on (test project, target framework)

Uniformly, **even for a single-targeted project**, so there is no special case in the shape. The TFM
is part of method identity, so two frameworks of one project can legitimately select differently
(`#if NET9_0`); collapsing them would force a union — safe, but needless over-selection — and it
destroys the answer to *"why did `net8.0` run this?"*. A pipeline that prefers one invocation can
coalesce; it cannot un-union.

Each entry carries:

| Field | Notes |
|---|---|
| `project`, `targetFramework` | the key |
| `framework`, `packageVersion`, `runnerHost`, `dialect` | all four are needed — package version changed xUnit's filter surface |
| `mode` | `filtered` · `run-all` · `skip` |
| `delivery` | `inline` · `response-file` · `testlist` · `chunked` · `runsettings` |
| `counts` | `selected`, `willRun`, `total` — and `total` **must be able to say `unknown`** |
| `tests` | §13.3; enumerated only when selected, unless `--list-unselected` |
| `invocations` | an **array** of argv vectors — many when chunked, one for `run-all`, zero for `skip` |

`total: unknown` is not cosmetic: reporting `0` for an unenumerable project would silently corrupt
§15's ratio. **Every test project appears even with an empty selection**, so a pipeline can skip it
deliberately rather than by absence.

### 13.3 Per selected test

- a **`rules` array** drawn from `reverse-reachable`, `own-source-changed`, `new-since-baseline`;
- for reverse-reachable tests, the **changes** that reached it, each with a **path class**;
- hop-by-hop **paths** only under `--paths` — the one genuinely unbounded thing in the document.

**Path class is the weakest edge provenance on the *strongest* path.** A test reachable by any fully
compiled path reads `compiled` even if a widened path also exists; one reachable only through
widening reads `widened`. This inverts naively taking the worst edge, and it is the right
instrument: `widened` means "over-selection is plausible here", which is what §15 needs and the only
class narrowing may ever touch.

**Both counts are carried.** The canonical selection and the rendered filter's true match set
deliberately disagree, because NUnit renders with `~`; the rendered set is obtained by applying the
dialect's own matching semantics back over the enumerated test list — cheap, since Reach already
enumerates every test method to produce `total`, and for M1 only NUnit over-matches, so it is one
substring pass. A `dialect-over-selects` notice names the extras when non-zero.

### 13.4 The change side is keyed on changes, not expanded roots

**Whole-assembly widening puts every method of an assembly into the changed set**, so one
unattributable file would expand into ten thousand roots and every test in that assembly's
dependents would carry all of them. So the change side is keyed on **changes** — a changed member,
or a file or artifact routed at a coarser tier — with the expansion left as an implementation detail
of the walk. A whole-assembly widening contributes **one** root: *"assembly `Acme.Orders` widened,
because `Acme.Orders.csproj` could not be attributed to a member."*

**The tier lives on the change, not on the (test, change) pair** — routing is a property of the
change, and a pair inherits it by reference.

**Forward-indexed alongside it: every change, with its tier and the count of tests it reached.**
This is arguably the most valuable field in the document. It makes *"I changed `OrderService.Submit`
and nothing runs"* a line you read rather than an absence you have to notice — either a genuine
coverage gap (useful) or an under-selection bug (critical), and today both are invisible.
Affordable because of the change-keying above: the list is diff-sized, not graph-sized. **Counts
only, never test names** — the pairs live once, on the test side — and a notice points at it when
any change reached zero tests.

If a root cap ever fires, it **keeps at least one change of every path class** before filling by
sort order, and always reports the true total: an arbitrary prefix could hide that a test was
reached *only* through widening, which is precisely the fact that decides whether to believe the
selection.

### 13.5 Unselected tests

Counted, not enumerated. Enumeration is opt-in via `--list-unselected`.

### 13.6 Naming a method

**Two fields: `display` and `id`.** The display form is fully-qualified type, method name, generic
arity, fully-qualified parameter types, assembly and TFM — derivable from *either* join candidate,
so the report does not presuppose §11.8's outcome. The contract asks only that `id` be stable within
a run and comparable across runs of the same tool version.

**Roots and path hops render as kernel methods**, never `<Submit>b__0_1` or `MoveNext`. A change
inside a state machine or lambda *is* a change to the kernel method, and a report naming
compiler-generated members reads as a bug to everyone who sees it. Where the compiler-generated
member is the interesting fact — a containment edge reconnecting an `async` body — it appears as a
detail *on* the hop, not as its name.

**Scope** enumerates test projects always, and in-scope assemblies as a flat list of identities
(name plus TFM). **Never closure edges**: those are the project graph and belong nowhere near a
report.

### 13.7 The envelope

Schema version, tool version, the baseline **as resolved to a SHA** (not `origin/main`) plus how it
was detected, the HEAD SHA, which change sources were included, the build mode, the forwarded build
argv, the §8 correspondence verdict, and **Reach's own phase timings** plus the build duration when
Reach ran the build. No test durations — Reach does not run tests.

The resolved baseline earns its place alone: the empty-change-set trap *is* a baseline that quietly
resolved to `HEAD`.

The timings and start time are **segregated into this one clearly-marked object** so that excluding
a single key makes two reports comparable (§13.9).

### 13.8 Where it goes, and versioning

Human summary to stdout, warnings to stderr, JSON to `.reach/report.json`; `--report -` sends JSON
to stdout and moves the summary to stderr.

**`schemaVersion` is a single integer, and the consumer's obligation is part of the contract:
tolerate unknown fields, unknown enum members and unknown notice codes.** That tolerance is what
makes new codes, kinds and outcomes additive; without it every code the register gains would be
breaking, which would strangle the notice design. Breaking means removing a field, repurposing one,
or changing its type. `toolVersion` stays separate — a bug fix is not a schema change. **M1 emits
version `1` and cannot emit any other.**

**A report is written even when the outcome is `failed`.** At the point of failure the envelope
holds exactly what is needed, and a tool that writes nothing when it fails is one you debug by
re-running it with more flags, on CI, which is the worst place to need a second run. The schema
therefore makes the selection-bearing sections **optional rather than required**. The one exception:
**a usage error writes no report**, because the arguments were never valid enough to establish where
to write one.

### 13.9 Determinism

**The report is byte-deterministic for a given input.** Every array sorted by a documented key:
projects by path, tests by fully-qualified name, changes by display form, notices by code then
locator. With the envelope's timings excluded, two runs over the same input produce identical
bytes.

**No size ceiling.** Report size correlates with the selection being surprising, so a ceiling would
strip the detail exactly when it is wanted.

### 13.10 The human summary is not a contract

Say so in the documentation, so nobody builds a `grep` pipeline against it. It carries the outcome,
per-entry mode and counts, notice counts by kind **with every `blind-spot` notice printed in full**,
the resolved baseline SHA and how it was detected, and where the report and side-car files went. Not
the selected test list.

Three things it must get right, because a normal run **is** the preview (§17 rules out a dry-run
verb):

- **The resolved baseline SHA and its detection source lead every run.** An audit line nobody reads
  does not catch a baseline that resolved to `HEAD`.
- **`no-changes` and `nothing-selected` get visibly different verdicts**, not two shades of "0
  tests". They exit the same and mean opposite things — a docs-only PR versus code changes that
  reached nothing, which is either a coverage gap or an under-selection bug.
- **Over-selection is stated as a percentage of the suite**, because that is the number that decides
  adoption.

When the outcome is `nothing-selected`, the reason codes **lead** the output. That is the result
most likely to be disbelieved.

---

## 14. Outcomes, exit codes and notices

### 14.1 Two closed sets

**Run-level `outcome`:** `selected` · `nothing-selected` · `no-changes` · `failed`.
**Entry-level `mode`:** `filtered` · `run-all` · `skip`.

There is no run-level "everything was selected" outcome — it is derivable from the modes, and a
second way to say it is a second thing to keep consistent.

**On `no-changes`, the full project list is still emitted**, every entry `skip` with zero
invocations, so a consumer's loop behaves identically across all four outcomes. Omitting entries
would make it a special case, and the consumer that forgets runs the full suite on an empty diff.

**Standing rule: an empty selection is never emitted without an accompanying reason list.**
`reasons` is a list of **notice codes**, not prose, required non-empty when the outcome is
`nothing-selected` — which means every cause of emptiness must be a documented, register-linked code
(`no-changed-member-reached-a-test`, `all-changes-outside-analysis-scope`,
`changes-were-formatting-only`). The emptiness taxonomy becomes enumerable and testable rather than
a place people add ad-hoc strings.

Changes outside the analysis scope are **reported with a category and never selected on** — *outside
any project*, or *inside a project no test project's closure reaches*. The second reads as *"nothing
in this repository can test this code"*, which is the honest answer for an untested worker or
console project and better than a warning. Whole-suite selection on an unattributed path would fire
on a docs-only PR, which is the exact change Reach exists to shrink.

### 14.2 Exit codes

| Code | Meaning |
|---|---|
| `0` | success — **including an empty selection** |
| `1` | usage error — the one case that writes no report |
| `2` | build failed |
| `3` | assembly missing, or assembly discovery failed (§6.2) |
| `4` | baseline unresolvable, including a shallow clone |
| `5` | source–binary correspondence failed |
| `70` | internal error |

**An empty selection exits 0.** Non-zero means *"do not trust my answer"*, which makes PRD §8's
invariant operational as one line of pipeline guidance: **if Reach exits non-zero, run the whole
suite or stop the build.** That line is inlined in the README as well as the exit-code table — the
one deliberate duplication in the documentation set, because it is the single line whose absence
silently violates the invariant.

Codes are needed only for PRD §8's error rows: an unparseable project graph **widens** (success,
with `run-all` entries) rather than failing.

### 14.3 Notices

**A single global `notices` array, with locators.** Never notices on entries as well: an unmodelled
framework affecting three projects would be duplicated three times or split across two channels, and
a consumer would have to read both to be confident it had seen everything. The `projects` locator
gives the per-project view for free.

Each notice carries:

- a **stable kebab-case `code`, never renamed** — `unmapped-file-no-project`, not `REACH1042`. The
  numeric convention exists because compilers emit thousands of diagnostics needing terse bulk
  suppression; Reach will have dozens, they are read rather than suppressed, and a slug carries most
  of the explanation to the person staring at a surprising selection — and gives the register a
  natural anchor per hole.
- **one `kind`**, and no severity axis: `blind-spot` (may under-select) · `widening` (may
  over-select) · `scope` · `environment`. A `warning`/`info` axis alongside this could only ever
  disagree with it, and a pipeline that wants to gate gates on `blind-spot`.
- a human **`message` that is complete on its own**, so a consumer ignoring `data` loses structure
  but never meaning.
- **`data`, free-form per code**, documented in the code index. The names `projects`, `assemblies`,
  `paths`, `members` are **reserved and used consistently** wherever they apply, which is what lets
  a consumer build "everything affecting this project" without knowing every code. Typing each code
  in the schema would make every new code a breaking change.

**Every `blind-spot` code must have an entry in [docs/limitations.md](../../docs/limitations.md),
and Reach's own test suite asserts the two sets match exactly.** A new blind-spot code fails the
build until it is registered. That single test is what converts §1.2's condition from a promise into
a build failure, and it is the whole mechanism. It runs in memory over the notice catalogue; no
fixture needed.

**No notice suppression in M1.** A suppression switch un-surfaces exactly the holes the bargain
depends on surfacing, and there is no evidence yet about which codes are noisy. If it ever ships,
`blind-spot` must be non-suppressible.

The register is **not generated** in either direction, and the spec **references** it rather than
restating its twenty-three entries: the report says *this run hit this gap*, while the register
explains what the gap is, which way it fails and how it closes. What binds them is the code.

---

## 15. The over-selection measurement

**A field in the report, not a harness.** Every input is already carried for other reasons, so M1
adds arithmetic rather than machinery.

- **Numerator: tests that will run**, not tests selected. The NUnit `~` rendering makes what runs a
  strict superset, and a deliberate dialect-level over-match *is* over-selection; reporting only the
  canonical count would understate Reach's real cost. **Both numbers are in the report and the gap
  between them is itself reported**, since it is the price of one named decision.
- **Denominator: enumerable test methods in in-scope test projects.** Unweighted.
- **`unknown` is reported as `unknown`.** A project on an unrecognised framework contributes to
  neither numerator nor denominator, and the summary carries **two numbers**:

  > `selected 312 of 1,840 tests (17%) · 2 projects on unrecognised frameworks run in full`

  No estimated denominator, no silent zero — treating an unenumerable project as zero would flatter
  the ratio in exactly the case where Reach is running an entire project.
- **The widening delta is the cheap count** over (test, change) pairs whose path class is `widened`.
  **No second walk in M1.** The rigorous version — the walk with widened edges and without — is a
  genuinely different measurement, because a test with both a compiled and a widened path reads
  `compiled`, so the cheap number *understates* widening's contribution. Registered as a measurement
  limitation, with the second traversal as the upgrade path.

**No replay harness in M1.** Replaying historical pull requests against each merge-base is the
honest version and is a separate tool; M1's job is to make every run emit the number so that a
harness — or a month of ordinary use — accumulates them for free. M1's own measurements are smoke,
not evidence: the fixture solution and Reach's own repository.

**The stop condition, fixed before any number exists**, which is the only way it stays credible:

| Median PR selects | Verdict |
|---|---|
| under 40% | on target |
| 40–70% | works, but not sellable without framework models or narrowing; M2 is justified and M1 was worth building |
| **over 70%, mostly `widened`** | **stop.** Widening through interfaces has eaten the value, and coverage-based selection is the better answer for that codebase |
| over 70%, mostly `compiled` | **not Reach's failure.** The codebase is too connected for any test-impact analysis; report it and walk away |

The last row is the one worth keeping, because it is the case most likely to be misread as a Reach
failure and to spend a quarter on models that cannot help.

---

## 16. Code layout, testing and the budget

### 16.1 Projects, ports and seams

```
Reach.Cli      → System.CommandLine 2.0.x; references Reach.Core
Reach.Core     → Microsoft.CodeAnalysis.CSharp 5.9.0, System.Reflection.Metadata
Reach.Tests    → one test project; InternalsVisibleTo from Core
```

**The tool targets `net10.0`**, single TFM, with `RollForward: LatestMajor` — which is kept and is
easy to lose in this decision rather than because of it: the default policy is `Minor` and will not
cross a major version, so without it a `net10.0` tool fails on a machine carrying only .NET 11. The
accepted cost is that Reach does not run on an agent carrying only .NET 8; roll-forward never goes
downward. Both packages resolve native `net10.0` assets, so there is no facade chain and no
`NU1605` class to manage.

**No `Reach.Contracts`** — PRD §6.2 forbids publishing it until the built-in models have shaped it,
and an unpublished contracts project is a shape nobody is pushing back on. It arrives in M2 with its
first consumer.

**One test project.** With §16.2's in-memory-first rule the fast tests are the overwhelming
majority, and a filter on the four slow ones is cheaper than a project boundary.

**`IProcessRunner` is the only port.** It covers `git` and `dotnet build` — two adapters at one seam
— and its fake is what makes §7's five CI environment variables, a shallow clone and a failed build
testable without any of those things being true. A deep module: a small interface (run an argument
vector, get exit code and streams) hiding process lifetime, cancellation, stream draining and
exit-code handling.

**The metadata reader is a parameter, not a port**, and this is the most consequential call in the
layout. `Reach.Core` accepts already-opened `MetadataReader`/`PEReader` instances, so an in-memory
Roslyn compilation emitted to a `MemoryStream` and a file on disk are *the same type*. There is
nothing to fake, because the BCL type already **is** the seam; an `IAssemblyReader` wrapping it
would be an interface nearly as complex as its implementation and would fail the deletion test.

**No filesystem abstraction.** Real temporary directories are less work than mock filesystem setup,
every filesystem test here needs a real `git` repository anyway, and the abstraction is viral —
it changes every signature that touches a path for a fake nothing needs.

**Seams that are modules, not C# interfaces:** the join (§11.8 — one adapter ships after the spike),
assembly discovery, filter rendering per dialect, and framework and runner detection. **Test
recognition is data** (§12.2) — the one place the seam genuinely varies, and a table is cheaper than
four adapters.

The deciding argument, and it is worth keeping: **Reach will be pointed at its own repository**, and
an interface with one implementation forever is exactly the indirection that makes widening fan out.

**Nothing in `Reach.Core` is public.** The CLI is the only contract, which is what makes
`report.json`'s schema version the compatibility promise rather than a type surface nobody meant to
publish. Roslyn is confined to `Reach.Core.Changes` **by convention**, not by a project split or an
architecture test — a deliberate omission, with an architecture test named as the cheap upgrade if a
second contributor arrives.

### 16.2 Testing, and M1's acceptance criteria

**Anything testable in memory is tested in memory.** In-memory Roslyn compilation runs in
milliseconds and can produce any IL shape on demand; a fixture solution has to build. Every IL shape
in §11 — async, iterators, lambdas, `static readonly` delegate fields, local functions, explicit
interface implementations, DIMs, static abstract members, accessors, sealed-type `using`,
interpolation, constrained and unconstrained generic receivers, the stack merge — is an in-memory
test compiled from a source string.
[assets/il-subject](assets/il-subject/README.md) is the starting corpus and was built for this.

**Integration fixtures earn their place only where the thing under test *is* the disk, the build,
the git history, or a real runner's behaviour.** Git history comes from a **temp repository per
test**: copy the fixture into a fresh temporary directory, `git init`, commit as the baseline, then
apply the change under test. A fixture committed in this repository has this repository's history,
which is not a usable baseline. This also makes §7's exit 4 testable — a shallow clone and a missing
merge-base are two `git` commands away.

**The four mandatory integration assertions.** These are end-to-end because each fails **silently**
if it regresses — no exception, no crash, just a wrong answer that looks plausible.

1. **The NUnit `~` filter, in both directions.** A parameterised test whose selection survives the
   rendered `FullyQualifiedName~` filter, under both the default configuration and
   `UseNUnitFilter=false`; *plus* a `MyTest`/`MyTest2` pair asserting the report's rendered-match
   count reports `MyTest2` as an extra. §15's headline metric reads that number, so a fixture that
   let it silently return zero would corrupt the measurement.
2. **An empty selection.** Report side: `mode: skip` with `invocations: []`. Consumer side, which is
   the half that earns the end-to-end slot: **a loop over `invocations` starts no process at all**,
   asserted by driving the loop and observing that no test command was issued. Two opposite failure
   modes are closed by the same fact — an empty filter runs everything, and under MTP an empty-match
   filter exits 8 and turns a green build red.
3. **Whole-project fallback** on a test project using an unrecognised framework: the project runs in
   full and the report says `total: unknown`, not `0`.
4. **Report determinism**: the same input twice, byte-identical once the timings envelope is
   excluded.

**Assertion 4 is what makes the rest cheap.** With determinism pinned, **exact expected selections
become the cheap option** rather than the brittle one — so assertions are exact selections, not
in/out sets.

**The negative assertions, each a named test**, because emergent coverage does not count:

- a **comment-only change** selects nothing — load-bearing now that §9.2 hashes whole declarations,
  so trivia-stripping has more to get right;
- a `Directory.Build.props` under a subdirectory **does not** select projects outside it — the
  assertion that proves §10.1's directory containment is real;
- a change reaching **no test at all** appears in the forward change list with a count of zero;
- a change reachable only through `Handler<Foo>` **does** select tests using only `Handler<Bar>` —
  asserting §11.1's accepted over-selection so it stays a decision;
- a changed **`const` consumed across an assembly boundary**, and a **removed overload** rebinding an
  untouched call site — §9.4's two triggers, both of which under-select if the rule regresses;
- **`[Fact]` added to an existing method** — the cheapest test of the most expensive regression in
  the set.

**Unit assertions that deliberately did not earn an integration slot:**

- **chunking** past the command-line ceiling — feed the chunker 200 test identities, assert the
  invocations partition the set with nothing dropped or duplicated;
- **the framework selector** — every emitted argv carries `-f` matching its entry's target framework
  where the project has more than one assembly instance. A missing selector fails **loudly** (exit
  8, with the offending module named in the runner's own summary), so it does not need a fixture;
- **the blind-spot/register parity test** (§14.3);
- **the docs↔workflow parity test** (§16.4).

**Layout tests are cheap** now that discovery scans rather than predicts: relocate already-built
output rather than needing a project per layout, and include the ambiguity error firing when Debug
and Release both exist.

**The fixture solution keeps as structure** what is a project-file fact rather than an IL shape: a
multi-targeted project, a `ReferenceOutputAssembly=false` reference, and a source generator project.
It needs to be realistic enough to exercise discovery at all.

### 16.3 The performance budget

**M1 commits to no wall-clock figure against a stated solution size.** There is no measurement yet,
so any number written now would be invented, and an invented number in a spec becomes a gate someone
later fails for no reason.

What M1 commits to instead is checkable:

1. **The two PRD §9.4 algorithmic constraints, asserted by tests rather than by review.**
   `sizeof(MethodId) == 8` and no string-typed field in the node or edge structures; and the type
   hierarchy index built **exactly once per run**, asserted with an instrumented construction counter
   over a fixture.
2. **The report's phase timings, always emitted** — the instrument already exists at zero extra cost.
3. **The first real run is the first datapoint**, and the number gets set from evidence.

A test that doubles a fixture and asserts sub-quadratic growth would be flaky in CI and is **not**
adopted.

**Deliberately not optimised in M1**, so implementation tickets do not gold-plate and a reviewer does
not read these as oversights:

- **No concurrency of any kind.** Graph construction is single-threaded. Assembly reads are the
  obvious parallel seam and are left for when there is a number to improve.
- No incremental or persisted graph — settled by PRD §9.3 and ADR-0001.
- No memory-mapped assembly reads, no object pooling or custom allocators, no struct-of-arrays node
  layout, and no string interning beyond the resolution index, which is rebuilt every run.

### 16.4 Documentation, CI and publishing

**Four documents, one of which already exists**, and no new machinery:

| Document | Reader |
|---|---|
| `README.md` | someone deciding whether Reach is for them — pitch, a two-line quickstart, the pipeline rule, then links |
| `docs/adopting-reach.md` | someone wiring a pipeline against PRD §12's under-an-hour clock — install, CI, **the exit-code table**, and the switches whose behaviour surprises |
| `docs/report-schema.md` | someone parsing `report.json`, and someone staring at a selection they do not believe — field reference, **the complete notice-code index**, the compatibility promise, and *"Reading a surprising selection"* walking the committed example |
| `docs/limitations.md` | already exists; unchanged in shape |

**No CLI reference document** — eleven options are `--help`'s job, and a second copy would drift from
the parser. **No "how Reach decides" explainer** — one README paragraph is it. Both are *dissolved*
rather than deferred, recorded so nobody re-adds them.

**One index for notice codes, in the schema reference.** The register keeps its identity and holds
accepted *holes*; `docs/report-schema.md` carries a complete index — full entry (meaning, `data`
keys) for codes that are not limitations, one line plus a link for codes that are. Blind-spot codes
appear twice, as index line plus zoom. Nothing adds a second parity obligation for the index.

**No JSON Schema file.** The contract is deliberately tolerant (§13.8), and a schema document would
have to be permissive everywhere the promise lives. The instrument instead is **a committed example
`report.json` that is the output of §16.2's determinism fixture**, so the test already written pins
the documentation too. Revisit trigger: a consumer asks to validate, or the first `schemaVersion`
increment.

**One verified CI recipe plus a table, not six recipes.** M1 ships one complete GitHub Actions
recipe and a table with a row per provider giving its fetch-depth setting and detection variable.
**Adoption cost is judged, not measured** — nothing rides on it the way §1.2's bargain rides on
correctness — with **one carve-out**: ADR-0010 makes exit 4 the likeliest first run anyone has, so
the fenced YAML block in `docs/adopting-reach.md` must be **the same YAML Reach's own CI runs**,
asserted by a test comparing the block against `dogfood.yml`.

**The PRD stops being the manual.** The README's four adopter facts — static analysis, compiled
assemblies, over-selects on purpose, never narrows — are one paragraph; the PRD link stays,
relabelled as background.

**Three workflow files in two phases.**

- **`ci.yml`** — buildable from the first commit. Push to `main` now, gaining a `pull_request`
  trigger at first publish. Matrix `ubuntu-latest` × `windows-latest`. Restore → build
  `-warnaserror` → `dotnet format --verify-no-changes` →
  `dotnet test -c Release --no-build --report-xunit-trx --results-directory ./TestResults`.
  **Both operating systems, and this is the one place worth the minutes**: Reach identifies a
  first-party assembly by the source documents its debug symbols point at and routes a changed file
  by matching those paths against the working tree, so separators and case sensitivity are exactly
  where Windows and Linux differ — and the tool is developed on Windows. A path-comparison bug on
  Linux is silent under-selection. This is the correctness rule, not thoroughness.
- **`release.yml`** — on a `v*` tag push: build, test, pack, push to **NuGet.org** via **Trusted
  Publishing** (`NuGet/login@v1`, `permissions: id-token: write`), since new API keys are capped at
  30 days. GitHub Packages is not an option: its NuGet feed requires authentication regardless of
  whether the package is public, so a `dnx` quickstart would work only for its author. **The version
  is hand-edited** in `Directory.Build.props`, first value `0.1.0-alpha.1` — a published package
  **cannot be deleted, only unlisted**, so two deliberate human acts are the right friction for an
  irreversible one. No signing certificate: nuget.org repository-signs automatically, and
  registering one makes signing mandatory for the account from then on.
- **`dogfood.yml`** — from the first tag onward: `fetch-depth: 0`, `dnx dotnet-reach@<version>`, the
  selection run **alongside** a full suite that keeps gating. **Reach runs on Reach and does not
  gate**: gating M1 on its own selection is the unproven narrowing PRD §8 forbids, and the instrument
  that would justify it is shadow mode, which is M2. Running both *is* a miniature shadow mode, and
  the gap between them is the first real datapoint §16.3 defers the budget to. It is a separate file
  because the parity test compares a fenced block against **a workflow file**.

**`global.json` is mandatory, not a preference.** `xunit.v3` 4.0.0 resolves to `xunit.v3.mtp-v2`,
and on the .NET 10 SDK that package's MSBuild targets make `dotnet test` a **hard build error**
unless `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`. Adding the VSTest
packages back does not help. It also pins the SDK band with an explicit
`rollForward: latestFeature` — the explicit policy is the load-bearing half, because an omitted
`rollForward` defaults to `patch`, the narrowest there is.

**Three things the suite needs from the runner**, from the temp-repository mechanism: `git` identity
configured (`user.email` and `user.name` are unset on hosted runners and `git commit` fails without
them); a NuGet restore of the fixture solution's own packages; and `<OutputType>Exe</OutputType>` on
the test project, which MTP enforces with a bespoke MSBuild error.

**No `actions/setup-dotnet`** — both runner images already carry .NET 10 feature bands.
**`dotnet format` runs in CI and as a pre-commit hook**; the hook keeps it out of the way, CI is what
makes it true.

**Install guidance leads with `dnx dotnet-reach@<version>`, never `-g`.** The version pin is a
**requirement, not a preference**: `dnx` with a bare package id will not find a prerelease and fails
with a misleading "not found in NuGet feeds".

Deliberately not done, each for a stated reason: no coverage gate (a threshold nobody agreed on is a
step that always passes); no `.snupkg` or SourceLink (Reach reads *consumers'* symbols; its own are
not load-bearing); no Dependabot (two deliberately pinned dependencies); no path filters on the gate
(a required check that never reports blocks a PR permanently); no branch protection requiring
approvals (PRs exist here to set `GITHUB_BASE_REF`, not to find a reviewer who does not exist).

---

## 17. Out of scope for M1

Named so an implementation ticket does not quietly grow into one of them.

| Excluded | Why, and the revisit trigger |
|---|---|
| **Framework models and the contracts assembly** | M2. PRD §6.2 forbids publishing contracts until the built-in models have shaped it |
| **Unmodelled-framework detection** | M2 |
| **Shadow mode** | M2, and the instrument any narrowing would need |
| **Narrowing of any kind**, including mock-aware narrowing and container registrations | conflicts with PRD §8's invariant and §6.2's additive-only rule; detecting a mock proves a mock exists, not that the real implementation is absent |
| **Local mode** | M3, and only after a `dotnet watch` spike |
| **Previously-failed-test selection** | ruled out by ADR-0001; a pipeline concern |
| **MVID comparison** | needs the baseline's binaries, so a second build. Registered as the upgrade path for §9.4's residue |
| **MTP test-node UID emission** (`--filter-uid`) | would dissolve the length ceiling for MTP hosts, but UIDs come from a discovery pass Reach does not run and the in-process provider is `[Experimental]` and needs a code change in the consumer's test project — the infrastructure ask PRD §9.1 exists to avoid. Trigger: Reach running discovery for another reason, or the provider stabilising |
| **An `explain` verb** | PRD §12 requires a surprising selection to be explicable *without rerunning the tool*, which makes a re-analysing `explain` a criterion violation dressed as a feature — and makes the *report* the thing that satisfies the criterion. `--paths` ships so the data exists for whoever writes it. Trigger: the first real surprising selection the report alone fails to explain |
| **A dry-run or preview verb** | dissolved, not deferred: `select` does not run tests, so a normal run already *is* the preview. The requirement it carried became §13.10's over-selection percentage |
| **Rendering for the direct executable** | §12.3. Named out of scope with **no notice** — it would fire on nearly every xUnit v3 project and mean nothing. Trigger: an adopter whose CI runs test executables directly |
| **Notice suppression** | §14.3 |
| **A second walk for the widening delta** | §15 |
| **A replay harness for the measurement** | §15 |
| **A JSON Schema file, a docs site, an FAQ, a migration guide, five of the six CI recipes** | §16.4 |
| **The absorption test for deleted methods** | §9.3, registered so the analysis is not lost |
| **M0's build-to-test ratio measurement** | PRD §11, consciously waived by the owner |

---

## 18. Implementation tickets

The tickets live at
[`.scratch/m1-implementation/issues/`](../m1-implementation/issues/) and are numbered in the order
they land. Each is sized to one agent session and carries its own acceptance criteria.

**Staged so something runs end to end early.** The point of a walking skeleton is a thin path
through every architectural piece, not a complete component at a time.

| Stage | Tickets | What exists at the end of it |
|---|---|---|
| **A — the spine** | 01–13 | `dotnet reach select` runs end to end: a change selects tests over **compiled edges only**, emits `report.json` and runnable argv vectors |
| **B — correctness** | 14–18 | every widening rule, the tier ladder, the parse rules, notices and the measurement |
| **C — acceptance and release** | 19–22 | the fixture solution and its assertions, the four documents, publication, and Reach running on Reach |

**Stage ordering is integration order, not release order.** Stage A alone under-selects
enormously; nothing is published until stage C, and the gate is the whole set.

**Two hard ordering constraints**, both inherited rather than invented:

- **`ci.yml` is buildable from the first commit** and is ticket 01's business.
- **`dogfood.yml`, the docs↔workflow parity test and the fenced block in `docs/adopting-reach.md`
  are one unit of work that arrives with `0.1.0-alpha.1` and cannot be written earlier.** The
  documented recipe is a `pull_request` workflow installing a *published* package; you cannot
  dogfood before you publish, and it turns out you do not need to.

---

## 19. Where each decision comes from

Every claim above traces to exactly one record. Zoom the link for the reasoning.

| Area | Record |
|---|---|
| No persisted state; rule 4 removed | [ADR-0001](../../docs/adr/0001-reach-persists-no-state-between-runs.md) |
| Analysis scope = union of test-project closures | [ADR-0002](../../docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md) |
| Correspondence via PDB source checksums | [ADR-0003](../../docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md) |
| Edge provenance, three safety classes | [ADR-0004](../../docs/adr/0004-call-graph-edges-carry-provenance.md) |
| Change detection keyed on declared type | [ADR-0005](../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md) |
| A node is an IL method definition | [ADR-0006](../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md) |
| Widening targets the inferred receiver type | [ADR-0007](../../docs/adr/0007-widening-targets-the-inferred-receiver-type.md) |
| M1 accepts named under-selection | [ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md) |
| Private filter channels, not `.runsettings` | [ADR-0009](../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md) |
| Baseline auto-detection, no default-branch fallback | [ADR-0010](../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md) |
| A solution wins over a project | [ADR-0011](../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md) |
| Resolve at 80/20 | [ADR-0012](../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md) |
| The tool targets `net10.0` | [ADR-0013](../../docs/adr/0013-the-tool-targets-net10-0.md) |
| Recompilation widening | [ADR-0014](../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md) |
| Invocations pin the target framework | [ADR-0015](../../docs/adr/0015-invocations-pin-the-target-framework-where-it-is-derivable.md) |
| Dialects, runner detection, the NUnit hazard | [ticket 01](issues/01-filter-dialects-and-runner-detection.md) |
| Solution and project-file parsing | [ticket 02](issues/02-solution-and-project-file-parsing.md) |
| Packaging, `System.CommandLine`, `dnx` | [ticket 03](issues/03-tool-packaging-and-cli-library.md) |
| Deletions, moves, untracked, ignored files | [ticket 04](issues/04-change-set-edge-cases.md) |
| Node and edge definitions, containment, the address-taken rule | [ticket 05](issues/05-generics-delegates-and-function-pointers.md) |
| PRD v0.2 | [ticket 06](issues/06-prd-amendments.md) · [PRD.md](../../PRD.md) |
| The report contract | [ticket 07](issues/07-the-report-contract.md) — **read its `## Amended by ticket 22` section with its answer** |
| The CLI surface | [ticket 08](issues/08-cli-surface.md) |
| Identity, resolution, edge storage, the budget | [ticket 09](issues/09-method-identity-and-performance-budget.md) |
| Projects, the single port, seams | [ticket 10](issues/10-project-layout-and-ports.md) |
| Fixtures and acceptance criteria | [ticket 11](issues/11-fixture-catalogue.md) — **read its `## Amended by ticket 22` section** |
| Roslyn, the TFM, and misparses | [ticket 13](issues/13-tool-target-framework-versus-roslyn.md) |
| Scan-and-verify assembly discovery | [ticket 14](issues/14-assembly-discovery-under-ambiguous-output-layouts.md) — **read its `## Amended by ticket 22` section** |
| The unmappable-change rule table | [ticket 15](issues/15-the-unmappable-change-rule-table.md) |
| `dotnet test --affected-tests` | [ticket 16](issues/16-what-is-dotnet-test-affected-tests.md) |
| The over-selection measurement | [ticket 17](issues/17-defining-the-over-selection-measurement.md) |
| What counts as a changed member | [ticket 18](issues/18-what-counts-as-a-changed-member.md) |
| The limitations register | [ticket 19](issues/19-the-limitations-register.md) · [docs/limitations.md](../../docs/limitations.md) |
| The documentation set | [ticket 20](issues/20-what-documentation-m1-ships.md) |
| CI, publishing, dogfooding | [ticket 21](issues/21-ci-for-the-reach-repository.md) |
| Zero-match filters, the third host, the framework selector | [ticket 22](issues/22-zero-match-filters-and-the-third-runner-host.md) |
