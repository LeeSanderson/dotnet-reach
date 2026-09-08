# Change-set edge cases: deletions, renames, untracked files

Type: grilling
Status: resolved
Blocked by: (none)

## Question

The tiered resolution of unmappable changes handles the common cases. Several edges are
undecided, and each one can cause under-selection.

**Deleted methods.** A method removed in the working tree has no declaration to join, so
it produces no graph identity and the reverse walk has nothing to start from. Worse, the
tier that catches it — whole-assembly selection for a changed file with no changed method
— does not fire if *other* methods in the same file did change. What is the rule?

**Deleted files.** No document to look up in the debug symbols, since the symbols describe
the current build. Does a deleted file select the assemblies that previously contained it,
and how is "previously" established without baseline build artifacts?

**Renamed and moved files.** Git may report these as a rename with similarity, or as a
delete plus an add. Does Reach care about the distinction?

**Untracked files.** A new test file never `git add`ed is invisible to every `git diff`
variant, yet PRD §4.2 requires tests that are new since the baseline to be selected. The
charting session leaned toward including them via `git ls-files --others
--exclude-standard`, on the grounds that excluding them under-selects. Confirm, and decide
whether it is default-on or opt-in — it is the kind of default that surprises people.

**Changes outside the analysis scope.** A changed file that belongs to no project in any
test project's closure. Selecting nothing is correct by ADR-0002, but silently selecting
nothing is how a tool loses trust — what does the report say?

**Ignored files.** A change to a `.gitignore`d file that is nonetheless compiled or copied
to output.

Every answer must state which direction it errs in, and why that direction is safe.

## Answer

Two coarse responses are distinguished throughout, because conflating them is easy and they
have very different blast radii. **Whole-assembly widening** and **whole-type widening** put
every method of an assembly, or every surviving member of a type, into the changed set and
then run the normal reverse walk. Neither is **whole-project selection**, which selects
every test in a test project. All three are now in [CONTEXT.md](../../../CONTEXT.md).

### The ladder is routed per change, not per file

Each changed element climbs the tier ladder independently and the results union. Per-file
gating is the only version that can return less than the sum of its parts, and the case it
loses is the ordinary refactoring commit: a deletion sharing a file with an edit, where the
edit keeps the file on tier 1 and the deletion resolves to nothing. This is a property of
the ladder, so [ticket 15](15-the-unmappable-change-rule-table.md) inherits it.

### A deleted method widens its declaring type

Unconditionally. If the type is gone too, whole-assembly widening; if the file is gone, the
rule below. **Errs wide.**

The tempting position — a deletion with a first-party caller fails the build, and one
without a caller is inert — holds for the large majority but misses **rebinding**:

```csharp
class Money  // a class, not a struct
{
    public override bool Equals(object? o) => o is Money m && m.Pence == Pence;
}
```

Delete that `Equals`. Nothing referenced it by name, so compilation succeeds, and every test
asserting two equal `Money` values are equal now compares references. The same shape covers
a deleted `override` falling back to the base implementation, a deleted `operator ==` on a
class falling back to reference equality, a deleted overload re-binding an untouched call
site, and a deleted partial method implementation whose calls the compiler then elides.
Deletion did not remove a binding; it *rebound* one.

**Considered and deferred: an absorption test.** Declare a deletion inert unless a surviving
binding target could absorb it — it was an `override`, an operator or user-defined
conversion, a same-named method survives on the type or its base chain, it implemented an
interface member with a default implementation, or it was a partial method implementation.
This is answerable from the binaries, since the type hierarchy index built for virtual
dispatch (PRD §9.4) can be asked whether a same-named survivor exists, so it needs no full
Roslyn compilation. Its inert branch has a real proof: inert means no first-party caller,
which means *editing* that member would also have selected nothing, so a deletion declared
inert is exactly as safe as an edit.

Deferred anyway, because it is a proof in five clauses and each clause is a place to be
wrong in the under-selecting direction — the list needed separate reasoning about records,
partial methods and default interface implementations to be convincing, which is a bad sign
for a list that decides correctness. Unconditional whole-type widening has no clauses, and
its cost is smaller than it looks: a deletion almost always travels with edits to the same
type, which were going to select that type's tests anyway, so the marginal over-selection on
a real commit is frequently zero. Reach for the absorption test only if
[the over-selection measurement](17-defining-the-over-selection-measurement.md) shows
deletions dominating the waste.

Two consequences: deleting a `[Fact]` widens its test class, so the deleted test's siblings
get selected — harmless and arguably right. And a deleted *type* removed from a file that
survives has no identity in the current binaries, so it skips to whole-assembly widening.

### A deleted file is routed as the deletion of every type it declared

There is no file-level rule. Reach already parses changed files at both revisions, so
`git show <baseline>:<path>` yields the declared types, and the rule above already says what
a deleted type does. Per the union rule, a deleted file that also declared something outside
a type — assembly-level attributes, `global using` — additionally goes to tier 3; one
declaring no types at all goes only there, which is ticket 15's default row.

Whole-assembly widening still needs to name an assembly for a document that appears in no
PDB, and it does so by **directory containment**: the nearest ancestor project directory,
which reproduces the SDK's default `**/*.cs` globbing without running MSBuild. It errs wide
(a path excluded by `<Compile Remove>` over-selects) and is imprecise only for linked files
pulled in from outside the project directory — which, being an addition to the glob, only
loses coverage the ancestor rule was never going to have. That imprecision is now confined
to the genuine-deletion case, which is where it matters least.

The case that decided this against a flat directory-containment rule is the folder
reorganisation. Mapping `Foo/A.cs` → `Bar/A.cs` by path widens the whole project's assembly
for a change where a reviewer can see nothing happened — the kind of result that gets a tool
switched off (PRD §4.3). Routed through the type, `A` is still in the binaries, so it widens
that type and nothing more, and the add side was already contributing those methods anyway.

### Renames and moves are never detected

`git diff --no-renames`, and the changed set is matched on **declared type, keyed by
fully-qualified name plus arity**, not on path — see
[ADR-0005](../../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md). A moved
type is found on both sides, its members are compared, and only the genuinely changed ones
become roots. Git's similarity threshold and the pairing of a delete with a specific add
stop existing as concepts, which removes a tuning knob whose wrong setting under-selects.
Reach still reads the changed *paths* from git, since tiers 2 and 3 are path-based; it just
never asks git to interpret them.

FQN keying means a type moved between namespaces is a deletion plus an addition, so it
widens the assembly, where a pure folder move stays at whole-type widening. **Errs wide**,
and deliberately: a namespace is part of the metadata name, so the move changes what runtime
binding sees — serialization type discriminators, convention-based registration,
`Type.GetType` — and those are blind spots Reach cannot see into. The asymmetry will surprise
someone, so it belongs in the documentation, not only the spec.

This matching is name-keyed, which only sounds like it contradicts PRD §9.4's "identity must
be integers derived from metadata tokens, never strings". It happens on the source side, over
the changed files only, and before the **join**. The graph side stays integer-keyed.

### Untracked files are always included, with no flag

Via `git ls-files --others --exclude-standard`. Opt-in under-selects by default and is out
under PRD §8; a `--no-untracked` escape hatch is a switch whose only possible effect is to
cause under-selection, and the pathological case it would exist for — a large untracked
directory bloating the selection — has a better fix in the consumer's `.gitignore`, which is
where "don't look at this" belongs. Under PRD §12's hour-to-adopt criterion, a flag that can
only make Reach wrong is a poor use of the option budget. **Errs wide.**

An untracked file has no baseline revision, so every type it declares is an added type and
every member a root; no special case needed. `--exclude-standard` is load-bearing rather than
tidiness: without it, every generated `.cs` under `obj/` returns as untracked and each
project's assembly widens on every run. An untracked `.cs` file in CI is worth a report line
— it is either intentional codegen or a forgotten `git add`, and both are things the reader
wants to know before trusting a surprising selection.

Concretely the change set is two commands:
`git diff --no-renames --name-status <merge-base>`, which spans committed, staged and
unstaged in one shot, plus `git ls-files --others --exclude-standard`.

### Changes outside the analysis scope are reported, never selected on

Exit behaviour unchanged. Whole-suite selection on an unattributed path would fire on a
docs-only PR, which is the exact change Reach exists to shrink. A distinguishable exit code
is really a CLI question and goes to [ticket 08](08-cli-surface.md); what this ticket fixes
is that an empty selection is a distinguishable *outcome* carrying its reason.

Three facts the report owes, with the schema left to
[the report contract](07-the-report-contract.md):

1. **The analysis scope itself** — which test projects, and which assemblies their closures
   cover. ADR-0002 permits pointing Reach at a single test project, and "your change is
   inert" versus "you asked me to look at one project" are very different news that produce
   identical selections.
2. **Every unattributed path, with its category** — *outside any project* (a README, a
   workflow file) or *inside a project no test project's closure reaches*. The second is
   more valuable than a warning: *nothing in this repository can test this code* is a useful
   thing to learn, and it is the honest answer for the untested worker or console project
   whose `Program.cs` someone just edited and who will otherwise assume Reach is broken.
3. **An empty change set as its own named outcome.** This is the misconfiguration to fear
   most — a baseline resolving to `HEAD` gives an empty diff, an empty selection, a green
   pipeline and no tests run, while every other guard (solution parsed, assemblies found,
   ADR-0003 checksums verified) passes happily. It is legitimate when a branch has no changes
   against its target, so it is not an error, but it must never be indistinguishable from a
   normal run.

Standing rule: **an empty selection is never emitted without an accompanying reason list.**

Related hazard, owned by tickets 07 and 08 and noted on both: an empty selection must not
render as an empty `--filter` string, because that runs everything.

### Ignored files: a documented blind spot, detected and reported

The problem is narrower than the question assumed. `.gitignore` governs only *untracked*
files — a tracked file that a later ignore rule covers is still tracked, and `git diff`
reports it normally. The blind spot is precisely **untracked and ignored**, and in CI most of
that population is derived rather than authored: `obj/`'s generated `AssemblyInfo.cs` and
friends are consequences of tracked inputs, so a change to them always has a visible cause.
What remains genuinely invisible is a pipeline step generating code from a source outside the
repository, and, once local mode arrives, a developer's ignored authored file.

No selection rule can fire on a change Reach cannot observe, so this is accepted and
documented — but it is also *detected*. ADR-0003 already enumerates and hashes every
first-party PDB document, which classifies each one three ways: on disk and tracked
(visible), on disk but untracked-and-ignored (**invisible**), or not on disk at all
(compiler-generated, ADR-0003's skip rule). The second category is reported per project.

**Report only, never select.** Selecting on it would widen every project's assembly on every
run, because `obj/` lands in that category for every project — which also means the check
must suppress documents under an `obj/` or `bin/` path segment, an approximation Reach is
stuck with because it does not run MSBuild and cannot read `IntermediateOutputPath`. That
path heuristic can only ever cost a warning, never a test, so it is not load-bearing on
correctness. Ignored *content* copied to output — a gitignored `appsettings.Development.json`
— has no PDB document and no cheap tell, so it stays purely documented.

One asymmetry for the documentation, because it inverts what people expect: **`--no-build`
catches the compiled case that default mode misses.** If an invisible source file changed
after the binaries were built, its ADR-0003 checksum no longer matches and Reach errors out;
in default mode the build quietly recompiles it and every checksum agrees. The mode that
trusts the caller more is the mode that detects more.

### Surfaced, and deliberately not answered here

[What counts as a changed member, and cross-assembly recompilation
effects](18-what-counts-as-a-changed-member.md). Hashing source member bodies cannot see a
change that alters how an *unchanged* body compiles: a changed `const`, enum member or
default parameter value is inlined into consumers across assembly boundaries, and removing
an overload re-binds untouched call sites. The line through it is that **additions are
self-covering, removals and re-valuings are not** — an added method is in the changed set and
the re-bound call site's IL points at it, so the reverse walk finds it for free, whereas a
deletion leaves the call site pointing at something nothing put in the changed set. A
different axis from this ticket, and its safe answer is expensive enough to need its own
grilling.

