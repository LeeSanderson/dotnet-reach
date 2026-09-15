# The join

Status: resolved
Depends on: 06, 07, 09
Spec: [§11.8](../../walking-skeleton/spec.md#118-the-join--deliberately-abstract), [§11.1](../../walking-skeleton/spec.md#111-a-node-is-an-il-method-definition)

## Goal

Turn a changed declaration in source into a `MethodId` in the call graph — by **measuring two
candidate mechanisms and picking one**, not by choosing on paper.

> **This ticket is a spike followed by an implementation, and the spec deliberately does not
> pre-empt it.** The design kept the **join** abstract from charting onward. Two mechanisms are
> live; both were judged plausible and neither was chosen, because the choice needs a
> measurement that only exists once there is code.

Ticket 09 lands first: the join produces `MethodId`s, so the identity representation and the
per-assembly resolution index must exist before either candidate can be measured.

## The spike

Build both, measure both on a fixture, write down the numbers, pick one, and **delete the
loser**.

**Candidate A — signature keys.** Derive a canonical signature from the Roslyn declaration
(fully-qualified declaring type, member name, generic arity, fully-qualified parameter types)
and look it up in the per-assembly resolution index ticket 10 already builds for cross-assembly
resolution.

**Candidate B — PDB line spans.** Take the changed declaration's source span from Roslyn and
find the method whose debug-symbol sequence points fall inside it. Ticket 08 already enumerates
every first-party document, so the symbol side is largely built.

**Measure, on the same fixture, for both:**

| Dimension | Why it decides |
|---|---|
| **Correctness on the hard shapes** | partial classes and partial methods; explicit interface implementations; overloads differing only in `ref`/`in`/`out` or by `modopt`/`modreq`; generic methods; nested and generic declaring types; local functions and lambdas; `file`-scoped types; records and their generated members |
| **Behaviour on a member with no IL** | an abstract or `extern` member, a `partial` declaration with no implementation |
| **Behaviour on a declaration the compiler did not emit** | trimmed by a conditional, or a phantom member from a misparse |
| **Cost** | time and allocations over the fixture solution, since the join runs once per changed member and the changed set is diff-sized |
| **Failure mode** | which direction does a *wrong* answer err in? |

The last row outranks the others. A mechanism whose failure mode is "no match, fall through to
whole-assembly widening" is far preferable to one whose failure mode is "matched the wrong
method", because the first over-selects and the second under-selects.

**Record the result in this ticket's Answer section** — the numbers, the choice, and what the
loser was worse at. A future reader will ask why one was picked.

## What the join owes, whichever mechanism wins

- **A changed property, indexer, event or operator declaration fans out to its accessors.**
  `get_Total`, `set_Total`, `op_Equality`, `add_Changed` are the nodes; the property is not.
- **An auto-property whose initializer changed maps to the constructor**, not to an accessor.
- **A field-like event's generated `add`/`remove` fan out the same way.**
- **A declaration that fails to join falls through to whole-assembly widening for its project.**
  Over-selection, and it is what makes a phantom member from a misparse survivable rather than
  a silent hole.
- **The report's `display` form is derivable from either candidate** — fully-qualified type,
  method name, generic arity, fully-qualified parameter types, assembly and TFM — so the report
  does not presuppose the outcome.
- **Roots render as kernel methods**, never `<Submit>b__0_1` or `MoveNext`. A change inside a
  state machine or lambda *is* a change to the kernel method, and a report naming
  compiler-generated members reads as a bug to everyone who sees it.

## Acceptance criteria

- Both candidates exist, are measured on the same fixture, and the numbers are in the Answer.
- One ships; the other is deleted, not left behind a flag.
- Every hard shape in the table above has a named in-memory test against the winner.
- A member with no IL, and a declaration the compiler did not emit, each fall through to
  whole-assembly widening rather than matching something else.
- A changed property selects tests reaching only its getter.
- An auto-property initializer change selects tests reaching the constructor.
- The seam is a function signature, not a C# `interface` — one adapter ships, so an interface
  would be indirection with one implementation forever, which is exactly what makes widening fan
  out in the codebase Reach will be pointed at.

## Out of scope

Changing the identity representation. Ticket 09 fixes the identity a join must *produce*; this
ticket decides how a declaration reaches it.

## Answer

**PDB line spans win. Signature keys are deleted.**

### The measurement

One fixture — the hard-shapes table, in one file, compiled in memory with real symbols — run
through both candidates. Both were debugged to their best available form first: the signature
candidate got a keyword table, an operator-name table, type-parameter wildcarding and
explicit-interface suffix matching before the final numbers were taken, because measuring a
half-built candidate would have proved nothing.

```
declarations: 21

               joined   missed    wrong  methods      build    resolve        alloc
span               17        4        0       21      5.12ms     0.051ms      18,040B
signature          19        2        0       21      0.20ms     0.118ms     117,688B
```

The counts alone read as a near-tie. What decides it is *which* declarations, and *what* each
candidate matched them to:

```
  N.IShape.Draw()             span: (none)                     signature: -IShape::Draw
  N.Hard.Overload(int)        span: Hard::Overload             signature: Hard::Overload
  N.Hard.Overload(long)       span: Hard::Overload             signature: Hard::Overload
  N.Hard.Overload(ref int)    span: Hard::Overload             signature: (none)
  N.Hard.Generic`1(T)         span: Hard::Generic              signature: Hard::Generic
  N.Hard.Draw()               span: Hard::N.IShape.Draw        signature: Hard::N.IShape.Draw
  N.Hard.Total                span: get_Total set_Total .ctor  signature: get_Total set_Total
  N.Hard.this[](int)          span: Hard::get_Item             signature: Hard::get_Item
  N.Hard.op==(Hard, Hard)     span: Hard::op_Equality          signature: Hard::op_Equality
  N.Hard.Changed              span: (none)                     signature: -add_Changed -remove_Changed
  N.Hard.WithLocal()          span: WithLocal <WithLocal>g__Helper|19_0   signature: WithLocal
  N.Hard.WithLambda()         span: WithLambda <>c__DisplayClass20_0::<WithLambda>b__0   signature: WithLambda
  N.Hard.External()           span: (none)                     signature: -Hard::External
  N.Hard+Nested`1.Abstract()  span: (none)                     signature: -Nested`1::Abstract
  N.FileScoped.Hidden()       span: <Hard>F977FCCC...__FileScoped::Hidden   signature: (none)
```

A leading `-` marks a match to a member with no IL at all: a join that contributes nothing to
the walk.

### Why span wins

1. **The failure-mode row, which the ticket says outranks the others.** An earlier run of the
   signature candidate produced a genuine wrong answer: `Overload(int)` matched a *different*
   overload. That is under-selection — the changed method's tests are not selected and another
   method's are. It took four separate heuristic fixes to stop it happening, and each fix is a
   place for it to come back. The span candidate never produced one, because position is not a
   heuristic: a sequence point either sits inside a declaration or it does not.

2. **A `file`-scoped type is unreachable by name.** Its IL type name carries a hash of the file
   path. No key derived from source can reconstruct it. This alone is disqualifying.

3. **Three of the things the join owes come free with position and would each need a rule
   otherwise.** The auto-property initializer maps to the constructor — span found `.ctor`,
   signature did not and has no way to. A local function and a lambda fan out from their kernel
   method — span found `<WithLocal>g__Helper|19_0` and the display-class method, signature found
   neither. Accessors fan out from their property.

4. **Reach has no semantic model.** Roslyn parses changed files and never loads a solution, so a
   parameter written `Widget` cannot be resolved to `Contoso.Parts.Widget`, a `T` cannot be
   matched to `!!0`, and `ref int` cannot be matched to `System.Int32&` — each needs another
   table, and each table is another way to match the wrong overload.

5. **Span's four misses are all members with no IL** — an interface method, an `abstract`
   method, an `extern` method, and a field-like event's generated accessors. Those fall through
   to whole-assembly widening, which is what the ticket specifies and is the safe direction.
   Signature "joins" all four, but to methods with no IL: the appearance of a join without the
   substance.

6. **Cost.** Span's build is 25 times more expensive (5.1ms against 0.2ms) and its resolve is
   2.3 times cheaper with 6.5 times fewer allocations. The build is O(methods) once; the
   signature candidate's resolve scans the whole index per declaration, so it is
   O(declarations x index) and gets worse as the solution grows. On a fixture this size the
   difference is noise either way.

### What the loser was worse at

Everything that is not a plain, uniquely-named, no-argument method. Signature keys handle
`GetHashCode()` and `Virtual()` perfectly well, and start guessing the moment a declaration has
parameters, type parameters, an operator spelling, an interface qualifier, a compiler-generated
partner, or a mangled type name.

## Comments

**Implemented** as `Reach.Core/Join/SpanJoin.cs`. `SignatureJoin.cs` and the spike harness are
deleted, not left behind a flag. The seam is a class with a `Resolve` function, not a C#
interface: one adapter ships, and an interface with one implementation forever is exactly the
indirection that makes widening fan out in the codebase Reach will be pointed at.

**Two things the spike changed outside this ticket.**

- **`MemberKey` now carries each parameter's passing mode.** `ref`, `out` and `in` sit on the
  parameter rather than on the type, so `M(int)` and `M(ref int)` — which C# does allow side by
  side — shared one key, and one of the two became invisible to change detection. That is a
  ticket 06 under-selection bug the spike found by accident; fixed in
  `SourceRevision.ParameterTypes`.
- **`ChangedMember` carries a `DeclarationSpan`**, one-based to match how debug symbols count,
  and `GraphAssembly` carries the PDB's own `MetadataReader`. Both are inputs the join needs and
  neither existed.

**An async kernel method does not join; its `MoveNext` does.** The kernel keeps no sequence
points of its own — its body is state-machine setup, marked hidden — so the join lands on the
generated `MoveNext`, which is where the changed code actually went. That is the right answer,
and it is also exactly why the containment edge from the kernel to the state machine is not
optional: without it the reverse walk from `MoveNext` dead-ends in the BCL. Named test.

**A record's primary constructor parameters produce no member declarations**, so a change to
them is a change to the *type header* and becomes whole-type widening. Safe direction, and no
gap — but worth knowing before someone goes looking for the missing rule.
