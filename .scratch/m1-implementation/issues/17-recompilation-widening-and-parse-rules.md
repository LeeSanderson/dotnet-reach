# Recompilation widening and the parse-failure rules

Status: resolved
Depends on: 06, 16
Spec: [§9.4](../../walking-skeleton/spec.md#94-recompilation-widening), [§9.5](../../walking-skeleton/spec.md#95-parsing-c-reach-does-not-know) · [ADR-0014](../../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md), [ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md)

## Goal

The two places where **source-level change detection is knowingly blind**: a consumer whose IL
changed while its source did not, and a file Roslyn parsed without understanding.

## Scope

### Recompilation widening

**Trigger — only these two:**

- a **removed** member of any kind;
- a changed **compile-time constant**: a `const` value, an enum member value, or a default
  parameter value.

**Effect:** whole-assembly widening for every in-scope assembly instance that **transitively
references** the declaring assembly, **through the project graph** — which is already built and is
far coarser than the call graph, so this is a walk over dozens of nodes, not millions.

**Why the declaring side cannot cover it.** The compiler bakes compile-time constants into every
consumer, so after inlining: the consumer's **source is byte-identical**, so nothing puts it in the
changed set; the consumer's **IL is different**, because the literal changed; and worse, the
consumer's IL **may no longer reference the declaring assembly at all**, since an inlined constant
leaves no trace of where it came from. So whole-assembly widening on the *declaring* assembly does
not reach a test that exercises the consumer. The same shape applies to a removed overload: an
untouched call site in another assembly rebinds to a different method and its source never changed.

**Additions trigger nothing**, and that is exactly what makes a narrow trigger sufficient. Adding
`Foo(int)` alongside `Foo(long)` rebinds `Foo(1)`, but the added member **is** in the changed set
and the call site's recompiled IL now points at it, so reverse reachability finds it for free. A
changed signature decomposes into a removal plus an addition, so the removal half fires.

**Two narrowings were considered and rejected. Do not re-add them.** Restricting to **public
surface** founders on `InternalsVisibleTo` and internal consts consumed by friend assemblies —
detecting the friend relationship correctly costs more than the widening saves. Including
**attribute-argument changes** in the trigger is unnecessary: an attribute argument lands in the
declaring assembly's own metadata and is **not** inlined into consumers, so ticket 06's
declaration-level hash already covers it.

**The blast radius is real and accepted because it is legible.** A `const` in a widely-referenced
core assembly can widen most of a solution — nothing else in Reach's design reaches that far from a
single change. Ticket 12's forward change list carries every change with its tier and the count of
tests it reached, so a pull request that selected everything shows exactly **which** `const` did
it. A user can then move the constant or accept the cost knowing why; both are better outcomes than
a quietly missing test.

**This makes the project graph load-bearing for correctness**, not just for scope resolution. A bug
in it is now an under-selection bug.

**MVID comparison stays out.** It is the honest general fix — it identifies every assembly whose IL
changed whatever the cause, including cases this rule misses — and it needs the baseline's
binaries, which means building a second revision. Registered as the upgrade path. The residue —
notably an agent's compiler version differing from the baseline's with no `global.json` change — is
a registered under-selection entry, undetectable.

### The parse-failure rules

**The naive rule — any parse error widens — is not a safety net.** Roslyn checks language versions
at **binding**, not parsing, and Reach only ever calls `ParseText`, so an older parser meeting newer
C# never throws and never loses round-trip fidelity. It fails three distinct ways:

| Outcome | Example | Reach sees it? |
|---|---|---|
| error diagnostic + local structural damage | a C# 14 extension block on an older Roslyn → `CS1513`/`CS1022`, skipped-tokens trivia, block mis-shaped as a constructor | yes |
| error diagnostic, structure intact | `\e` escape → `CS1009` | yes |
| **clean parse, structurally wrong, zero diagnostics** | `record Person(string First)` on an older Roslyn parses as a *method* named `Person` returning a type called `record` | **no** |

The third row is deliberate rather than a bug — contextual keywords are recognised only when the
feature is enabled. **A clean parse never proves a correct parse.** Four rules:

1. **Parse with `LanguageVersion.Preview`, never `Latest`.** The highest-value line here and it is
   one argument. Ticket 06 sets it; this ticket asserts it.
2. **An error diagnostic *or* `SkippedTokensTrivia` in a changed file triggers whole-assembly
   widening for its project, plus a `parse-failed` notice.** Skipped-tokens trivia earns its place
   because it is a **structural** signal rather than a diagnostic one, and row 1 produced it. Errs
   over.
3. **A project whose declared `LangVersion` exceeds Reach's parser ceiling gets a
   `langversion-above-ceiling` notice, not widening.** With current Roslyn shipped the window is
   narrow, and widening on it would fire across whole modern codebases for a hazard that usually is
   not present.
4. **The silent misparse is a register entry** — direction under-selection, partially detectable
   (rows 1 and 2 only), upgrade path *ship current Roslyn and exercise the parser against the newest
   SDK in CI*.

**The hole is narrower than it first looks, and the reason matters so nobody re-widens it:** the
**same parser reads both revisions**, so a deterministic misparse still diffs stably, and a phantom
member simply fails the join and falls through to whole-assembly widening — which over-selects. The
genuine hole is a construct whose members are **swallowed** from the tree, as in row 1, because a
changed method inside one never enters the changed set at all.

## Acceptance criteria

- **A changed `const` consumed across an assembly boundary selects tests in the consumer.** Named
  test — this is one of the two shapes that under-select if the rule regresses.
- **A removed overload rebinding an untouched call site in another assembly selects that
  assembly's tests.** The other one.
- A changed enum member value and a changed default parameter value both trigger.
- **An added overload triggers nothing**, and the call site is still reached — asserted, since the
  narrow trigger is only sufficient if this holds.
- **A changed attribute argument does not trigger recompilation widening**, but *is* a changed
  member.
- An `internal const` consumed by a friend assembly triggers — asserted, so nobody narrows to
  public surface.
- Widening follows the project graph transitively, not the call graph.
- The forward change list attributes the blast radius to the specific `const`.
- `LanguageVersion.Preview` is asserted in the parse options.
- A file with a syntax error and a file producing `SkippedTokensTrivia` each widen their project
  and emit `parse-failed`.
- A project declaring a `LangVersion` above the ceiling emits `langversion-above-ceiling` and does
  **not** widen.
- Every notice code introduced here that is of kind `blind-spot` has a register entry — enforced by
  ticket 18's parity test.

## Out of scope

MVID comparison. Any attempt to detect the silent misparse — it is a documented hole, and the
mitigation is shipping current Roslyn.

## Comments

**Implemented** as `Reach.Core/Changes/RecompilationWidening.cs` and
`Reach.Core/Changes/ParseHealth.cs`, consumed by `RootSets.ChangesFrom` and `ChangedSetBuilder`
respectively.

**Recompilation widening reads nothing about accessibility, and that is the point.** The
rejected narrowing to public surface would have needed a friend-relationship check;
`RecompilationWidening` never looks at a modifier, so an `internal const` consumed by a friend
assembly triggers exactly like a public one. There is a named test for it, so the narrowing
cannot be re-added without turning a green test red.

**Each triggering member is its own entry in the forward change list, carrying its own reason.**
That is what makes the blast radius accepted rather than merely tolerated: a pull request that
selected everything shows exactly which `const` did it, and the reader can move the constant or
accept the cost knowing why.

**Additions triggering nothing has its own assertion**, because the narrow trigger is only
sufficient if that holds — an added member is in the changed set and the rebound call site's
recompiled IL points at it, so the reverse walk finds it for free.

**The parse check runs over both revisions.** A baseline revision that does not parse is as much
a reason to distrust the members read out of a file as a working-tree one, and reading only the
current side would miss a file that *stopped* being unparseable.

**`ExceedsCeiling` reports and does not widen**, as the ticket requires. It returns false for
`preview`, `latest`, `default` and every numeric version the shipped parser understands — and
only a numeric version above the ceiling is reported, which with current Roslyn shipped is a
narrow window.

One thing worth naming because it is *not* implemented: nothing here detects the silent
misparse, and nothing can. `record Person(string First)` on an older parser reads as a method
with zero diagnostics. The mitigations are the two the ticket names — parse with
`LanguageVersion.Preview`, which is asserted, and ship current Roslyn — plus the structural
reason the hole is narrower than it looks: the same parser reads both revisions, so a
deterministic misparse diffs stably and a phantom member fails the join and falls through to
whole-assembly widening.
