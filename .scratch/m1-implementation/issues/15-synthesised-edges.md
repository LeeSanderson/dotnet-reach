# Synthesised edges: containment and type initialization

Status: resolved
Depends on: 09
Spec: [§11.5](../../walking-skeleton/spec.md#115-containment-and-why-it-is-not-optional) · [ADR-0004](../../../docs/adr/0004-call-graph-edges-carry-provenance.md)

## Goal

Reconnect the two places where control flow is certain but no instruction expresses it. Without
the first of them, **every `async` method body in the solution is unreachable** — under-selection
on ordinary modern C#, not an edge case.

## Scope

**Containment: kernel method → compiler-generated member.**

`async Task<int> Aw()` reaches its own body only through
`AsyncTaskMethodBuilder<int>::Start<'<Aw>d__0'>` — `MoveNext` is invoked from **inside the BCL**.
So a walk backwards from a change inside any `async` method body dead-ends in an assembly that is
not first-party and is not analysed.

The fix is **exact rather than heuristic**, and the attributes were verified present on the kernel
methods:

- `[AsyncStateMachine(typeof('<Aw>d__0'))]`
- `[IteratorStateMachineAttribute(typeof('<It>d__1'))]`

Each names the generated type directly. An edge runs from the **kernel method** to the generated
members, so a change inside them resolves to the method the developer actually wrote.

**The name-mangling convention is the fallback, not the primary mechanism** — `<Foo>b__0`,
`<Foo>g__Local|0_1`, `<>c__DisplayClass` — for closures the attributes do not cover.

Two things need no special rule, and knowing that keeps the ticket small:

- **Lambdas.** A lambda emits `ldftn '<>c'::'<Lam>b__2_0'` inside the kernel method, so ticket
  09's capture rule already covers it.
- **Local functions** are an ordinary `call`.

**Iterators differ in shape but not in outcome**: `It()` does `newobj '<It>d__1'::.ctor`, a real
compiled edge, but `MoveNext` still arrives via interface dispatch from the consumer's `foreach` —
so containment carries it either way.

**Type initialization: initialization trigger → `.cctor`.**

An edge is synthesised from every method that triggers initialization — `newobj`, static field
access, or static method access on the type — to that type's `.cctor`. It is cheap, because those
trigger instructions are **already being decoded** to find call edges.

It closes a real hole, verified rather than hypothetical: a `static readonly Func<int,int>` field
compiles to `ldftn` **inside `.cctor`**, which nothing visibly calls, so without this edge the
captured lambda is orphaned.

**Provenance: both kinds are class `synthesised`, and that class exists because of this ticket.**
ADR-0004 originally split provenance two ways, compiled and widened; containment and type
initialization fit neither. They are **invented by Reach, but the control flow they describe is
not in doubt**, so they are **as non-removable as compiled edges**. Do not tag them `widened`:
narrowing may only ever remove widened edges, and mis-tagging these would put a certain edge inside
a future narrowing's safe domain.

The same distinction sharpens the measurement: "the walk with widened edges and without" means
the four non-widened kinds versus all seven.

**Report rendering.** Roots and path hops render as **kernel methods**, never `<Submit>b__0_1` or
`MoveNext` — a report naming compiler-generated members reads as a bug to everyone who sees it.
Where a containment edge reconnecting an `async` body is the interesting fact, it appears as a
**detail on the hop**, not as its name. Ticket 12 owns the rendering; this ticket owns supplying
the kernel-method attribution.

## Acceptance criteria

Every one is an in-memory test compiled from a source string.

- **A change inside an `async` method body selects a test that awaits it.** The assertion this
  ticket exists for — without containment it fails, and it fails silently.
- A change inside an iterator body selects a test that enumerates it.
- A change inside a lambda body selects a test reaching the enclosing method, **via the capture
  edge rather than containment** — asserted so the two mechanisms stay distinguishable.
- A change inside a local function selects a test reaching its enclosing method.
- A change to a closure class the attributes do not name is reached via the name-mangling
  fallback.
- **A change inside a lambda captured by a `static readonly` field selects a test that uses the
  field** — the type-initializer assertion, and the case that would otherwise be orphaned.
- A `newobj`, a static field read and a static method call each produce a `.cctor` edge.
- Every edge this ticket creates carries **synthesised** provenance, asserted directly.
- Path class over a graph whose only route crosses a containment edge reads `compiled`-class
  trust, not `widened` — i.e. synthesised edges do not degrade a path.
- A root inside a state machine renders as the kernel method in the report, with the
  compiler-generated member appearing only as a hop detail.

## Out of scope

Widening (ticket 14). An eighth edge kind: the generator relation from ticket 16's rule table is a
**compilation-input** relation, not a call edge, and does not enter the graph.

## Comments

**Implemented** as `Reach.Core/Graph/SynthesisedEdges.cs`, called from the pass that already
exists. Both kinds carry `Containment` and `TypeInitialization` provenance, which
`EdgeProvenances.IsSynthesised` groups and `IsWidened` excludes — so no narrowing can ever
touch them.

**This ticket found a gap in ticket 09: `newobj` produced no edge at all.** Ticket 09's edge
kinds are `call`/`callvirt` and `ldftn`/`ldvirtftn`, and `newobj` is in neither list — so a
change to a constructor was unreachable from `new Foo()`, and an iterator's
`newobj '<It>d__1'::.ctor`, which the spec names as *"a real compiled edge"*, was not an edge.
Found by a display-class test whose `.ctor` had no callers at all. `newobj` now produces a
compiled call edge like any other constructor call.

**The name-mangling fallback keys on method names as well as type names, and it has to.** A
state machine puts the kernel's name in the *type* (`<Work>d__0`); a display class puts only an
ordinal there (`<>c__DisplayClass0_0`) and puts the name on its *members* (`<Make>b__0`). The
first version read only the type name and found nothing for any display class.

**A local function now carries a containment edge as well as its ordinary call edge.** The
ticket says local functions need no rule, and that is true of the call — but a local function
*is* a compiler-generated member of its kernel method, both edges point the same way, and
keeping it is the safe direction for any shape where nothing visibly calls one. One existing
assertion relaxed from "exactly this edge" to "this edge among them", with the reason recorded
in the test.

**Every acceptance criterion has a named in-memory test**, including the two that would
otherwise fail silently: a change inside an `async` body reaching a caller that awaits it, and
a lambda captured by a `static readonly` field reaching a caller that uses the field. The
second is the case the type-initializer edge exists for — `ldftn` inside `.cctor`, which
nothing visibly calls.

**Rendering roots as kernel methods** is ticket 12's, and is already satisfied from the other
side: the report renders from `ChangedMember`, which is the declaration the developer wrote, so
no `<Submit>b__0_1` or `MoveNext` can reach it. The hop detail for a containment edge is the
part still outstanding, and belongs with `--paths` rendering.
