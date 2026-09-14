# The changed set

Status: ready-for-agent
Depends on: 02, 03
Spec: [§9.1](../../walking-skeleton/spec.md#91-sources)–[§9.3](../../walking-skeleton/spec.md#93-deletions-moves-and-renames) · [ADR-0005](../../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md)

## Goal

From a baseline SHA, the list of changed **members** and changed paths, each ready to be routed
to a tier. Roslyn lives here and nowhere else.

## Scope

**Roslyn is confined to `Reach.Core.Changes`**, by convention rather than a project split or an
architecture test — a deliberate omission, recorded so a reviewer does not read it as an
oversight; an architecture test is the cheap upgrade if a second contributor arrives. PRD §9.2's
rule is absolute: Roslyn parses changed files and **never loads a solution**. No
`MSBuildWorkspace`, ever.

**Sources**, two commands:

```
git diff --no-renames --name-status <baseline>     # committed since baseline, staged, unstaged
git ls-files --others --exclude-standard           # untracked
```

**Untracked files are always included, with no flag.** Opt-in under-selects by default, and a
`--no-untracked` escape hatch is a switch whose only possible effect is under-selection.
**`--exclude-standard` is load-bearing rather than tidiness**: without it every generated `.cs`
under `obj/` returns as untracked and every project's assembly widens on every run. An untracked
`.cs` file entering the change set is worth a report line — either intentional codegen or a
forgotten `git add`, and both bear on trusting a surprising selection. An untracked file has no
baseline revision, so every type it declares is an added type and every member a root: no
special case needed.

**Parse both revisions.** The working tree from disk; the baseline via
`git show <baseline>:<path>`. Parse with **`LanguageVersion.Preview`, never `Latest`** — the
highest-value line in this ticket and it is one argument. Ticket 17 owns what happens when the
parse goes wrong; here, just get the flag right.

**Match declared types, keyed on fully-qualified name plus arity.** Paths are read from git for
the path-based tiers, but git is never asked to *interpret* them. A moved type is found on both
sides and only its genuinely changed members become roots. Git's similarity threshold and the
pairing of a delete with a specific add stop existing as concepts, which removes a tuning knob
whose wrong setting under-selects.

Simple name plus arity was rejected as the key: a namespace is part of the metadata name, so a
namespace move changes what runtime binding sees — serialization discriminators,
convention-based registration, `Type.GetType` — and those are blind spots Reach cannot see into.

**The unit of comparison is the whole member declaration with trivia stripped** — modifiers,
attributes, signature, parameter defaults, initializer and body. Not the body. Roslyn is already
parsing the file, so this costs nothing and closes the declaring-side list in one move: `const`
values, field initializers, enum members, attribute arguments and default parameter values all
sit inside the declaration. It also settles PRD §4.2 rule 3 — **adding `[Fact]` to an existing
method registers as a change** — so "new since the baseline" stays derivable from the change set
and needs no separate mechanism.

**Type headers hash separately.** Modifiers, attributes, base list and type parameters are their
own unit, and a change there is **whole-type widening**. This covers a test class gaining an
attribute that makes its methods discoverable, which no member-level comparison would see.

**Stripping trivia is what preserves "comment and formatting churn must not select."** That now
has to hold over a larger syntactic surface than a body hash, which is why it gets its own test
rather than being assumed.

**Deletions and moves**, each erring the direction stated:

| Change | Rule | Errs |
|---|---|---|
| a deleted **method** | whole-type widening on its declaring type, **unconditionally** | over |
| a deleted **type** (file survives) | whole-assembly widening | over |
| a deleted **file** | routed as the deletion of every type it declared at baseline | over |
| a deleted file declaring nothing inside a type | additionally routed to tier 3 (ticket 16) | — |
| a namespace move | delete plus add → whole-assembly widening | over |
| a directory-only move | found on both sides → whole-type widening | over |
| a rename | delete plus add; a rename that also moves members can leave a gap | **under**, registered |

**Why a deleted method widens unconditionally**, since the tempting alternative looks safe and
is not: a deletion can **rebind** rather than remove a binding. Delete an
`public override bool Equals(object? o)` from a class and nothing referenced it by name, so
compilation succeeds and every test asserting two equal values are equal now compares
references. Same shape for a deleted `override` falling back to the base, a deleted `operator ==`
on a class falling back to reference equality, a deleted overload re-binding an untouched call
site, and a deleted partial method implementation whose calls the compiler elides.

**Do not implement the absorption test.** It is designed, registered and deliberately not
adopted: a proof in five clauses, each a place to be wrong in the under-selecting direction.

Whole-assembly widening must name an assembly for a document that appears in no PDB, and does so
by **directory containment** — the nearest ancestor project directory, reproducing the SDK's
default `**/*.cs` globbing without running MSBuild. Errs over.

**Report the untracked-and-ignored blind spot but never select on it.** Ticket 08's document
enumeration supplies the classification; this ticket consumes it. Selecting on it would widen
every project's assembly on every run because `obj/` lands there for every project — which is
also why the check suppresses documents under an `obj/` or `bin/` path segment, an approximation
Reach is stuck with because it does not run MSBuild and cannot read `IntermediateOutputPath`.
That heuristic can only ever cost a warning, never a test.

## Acceptance criteria

- A comment-only change produces an empty changed set. **Named test**, over a declaration
  carrying attributes and an initializer, not just a body.
- A whitespace-only reformat of a whole file produces an empty changed set.
- `[Fact]` added to an existing method **is** a changed member. **Named test** — the cheapest
  test of the most expensive regression in the set.
- A changed `const` value, enum member value, default parameter value, field initializer and
  attribute argument each register as changed members.
- A type moved between directories yields whole-type widening; a type moved between namespaces
  yields whole-assembly widening. Both asserted, since the asymmetry is deliberate.
- A deleted method widens its declaring type; a deleted type widens the assembly; a deleted file
  routes through its baseline-declared types.
- Untracked files are in the change set; files under `obj/` are not.
- A type-header change yields whole-type widening.
- Change detection reads deleted and moved files at the baseline revision.

## Out of scope

Routing an unmappable change — ticket 16. Recompilation widening and the parse-failure rules —
ticket 17. Turning a changed member into a `MethodId` — ticket 09.
