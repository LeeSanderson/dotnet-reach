# Widening: the type hierarchy index and receiver-type inference

Status: resolved
Depends on: 09
Spec: [§11.7](../../walking-skeleton/spec.md#117-widening) · [ADR-0007](../../../docs/adr/0007-widening-targets-the-inferred-receiver-type.md), [ADR-0006](../../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md)

## Goal

Edges from a dispatch declaration to every implementation it could reach — **bounded by the
inferred receiver type**, so the graph carries information rather than connecting everything to
everything.

## Scope

**The type hierarchy index is built in exactly one pass**, during the metadata pass that already
exists. Resolving implementations per call site is accidentally quadratic and *will look fine on
a sample repository and fail on a client's*. This is one of the two algorithmic constraints M1
asserts by test rather than by review.

**Widening edges hang off the dispatch declaration node**, never duplicated at every call site —
ticket 09 already creates those nodes. Smearing widened edges across the graph would leave them
neither isolable nor countable, which breaks the measurement.

**Widening is bounded by the inferred receiver type** — the static type a dispatch site can be
shown to hold — recovered by a **single-pass abstract interpretation of the evaluation stack
tracking static types only**, inside the pass already decoding call instructions. A future reader
will reasonably ask why there is an abstract interpreter in what should be a metadata reader; this
is why.

**Why the instruction's own token is not enough**, and this was verified by disassembly rather
than assumed: **Roslyn emits `callvirt` against the slot-defining declaration**, not the
most-derived statically-known type. `x.ToString()` where `x` is statically a type that *declares
an override* still emits `callvirt System.Object::ToString()`, and `using var r = new Res()` on a
**sealed** class implementing `IDisposable` emits `callvirt System.IDisposable::Dispose()` rather
than a direct call. Taking the token at face value makes fan-out maximal: every `ToString()` call
site edges to one `object::ToString` node which widens to every override anywhere, so changing any
one override reverse-reaches almost the entire suite. That is not conservatism, it is a graph with
no information in it.

**The resolution ladder, in order:**

1. a `newobj` / `castclass` / `isinst` / `unbox.any` operand — an **exact** type
2. an `ldarg` / `ldloc` / `ldsfld` / `ldfld` signature type
3. the `callvirt` token itself
4. the slot-defining declaration

Rung 3 is stronger than expected: `Constrained<T>(T x) where T : IShape` emits
`constrained. !!T; callvirt IShape::Area()`, so **Roslyn puts the generic constraint in the
token** and inference gets it without reading the constraint table.

**This is not narrowing**, and it is worth being able to say why: a variable of static type `Sq`
cannot hold a non-`Sq` in verifiable IL, so the bound comes from the type system rather than from
inference about runtime behaviour, and no edge that could run is removed.

**Mapping.** **`MethodImpl` is authoritative** for interface member → implementation, because an
explicit interface implementation is deliberately *not* name-matchable; name and signature matching
is the fallback for implicit implementations. A **default interface method** is a real body on the
interface, so widening from an interface node reaches the DIM body **alongside** every override.

**Static abstract interface members widen to every implementer.** The implementation is selected by
the generic instantiation at the call site — precisely what ticket 09's identity discards — so
nothing narrower is available, and the widening direction is the safe one. Errs over, registered,
and the most visible cost of collapsing instantiations.

**`ldvirtftn` produces widened edges to the overrides** as well as its compiled capture edge.

**Three residues, all bounded, all erring over, all registered.** Do not attempt to close them:

- a **stack merge** — a ternary compiling to branches pushing different types — takes the common
  supertype, so that one site keeps full fan-out;
- an **unconstrained generic receiver** emits `constrained. !!T` with the token at
  `object::ToString`, and identity erases `T`, so it falls to rung 4;
- an **interface-typed receiver** infers to the interface — correct and useless. **The DI shape
  *is* an interface-typed receiver, so PRD §11's main technical risk is untouched by this ticket**
  and remains unmeasured until ticket 18's number exists. Do not claim otherwise in a commit
  message.

**Provenance.** Every edge this ticket creates is class **widened** — the only speculative class,
and therefore the only class any future narrowing may touch. Do not tag containment or
type-initializer edges (ticket 15) as widened; they are synthesised and as non-removable as
compiled ones.

**Cost.** This puts the only per-instruction work in the graph builder's hot loop. It is linear in
IL size and rides a pass that already exists, but it is not free, and the phase timings should show
it.

## Acceptance criteria

Every one of these is an in-memory test compiled from a source string;
[`assets/il-subject`](../../walking-skeleton/assets/il-subject/README.md) is the starting corpus
and was built for exactly this.

- **The type hierarchy index is built exactly once per run**, asserted with an instrumented
  construction counter over a fixture. One of M1's two committed algorithmic constraints.
- Each rung of the ladder has a test: a `newobj` receiver (exact), an `ldsfld` receiver, a
  constrained generic receiver picking the constraint from the token, and a fall-through to the
  slot-defining declaration.
- `x.ToString()` on a type declaring an override widens to **that type's override only**, not to
  every override in the graph. This is the assertion the whole ticket exists for.
- A sealed type's `using` widens to that type's `Dispose`, not to every `IDisposable`.
- An **explicit** interface implementation is reached via `MethodImpl`, not by name matching.
- A default interface method body is reached **alongside** overrides, not instead of them.
- A static abstract interface member widens to every implementer.
- A stack merge keeps full fan-out at that site, asserted so the residue stays a decision.
- An interface-typed parameter infers to the interface — asserted, so nobody later believes this
  ticket solved the DI case.
- Every edge created carries `widened` provenance, and path class over a graph with both compiled
  and widened routes reads `compiled` (ticket 11's rule).

## Out of scope

Narrowing of any kind. Containment and type-initializer edges — ticket 15. Any attempt to bound
the interface-typed receiver — that is what framework models are for, in M2.

## Comments

**Implemented** in `Reach.Core/Graph`: `TypeHierarchy` (the index), `ReceiverTypes` (the
abstract interpreter) and a `Widen` pass on `CallGraphBuilder`.

**The ladder works by re-anchoring the call site's edge, not by adding edges at the call
site.** Given an inferred receiver type, `TypeHierarchy.SlotFor` finds the type that declares
the member as seen from there, and the compiled edge points at *that* node; widening then hangs
off it, computed once per node. So `square.ToString()` edges to `N.Square::ToString` rather
than to `object::ToString`, and widening from it reaches Square's subtree and nothing else.
That is the assertion the whole ticket exists for, and it has a named test with a sibling class
asserted *not* reachable.

**Two things had to be true at once that the ticket states separately.**

- **`callvirt` and `ldvirtftn` are dispatch whatever the declaring type is.** The slot is
  routinely `object::ToString` or `IDisposable::Dispose`, in an assembly Reach never reads, so
  a check against the first-party hierarchy would have refused to widen exactly the cases the
  ticket names.
- **A static abstract interface member dispatches with `call`, not `callvirt`.** The opcode
  therefore cannot be the test on its own, and widening every `call` would reach a subtype's
  `new`-shadowed method, which never runs from that site. `TypeHierarchy.IsDispatchable` —
  interface member, or virtual, or abstract — is what keeps both true. The static-abstract test
  failed on exactly this before the rule was split.

**The stack is cleared at every branch target and every exception-handler entry.** That is a
linear pass rather than a worklist over basic blocks, and it *is* the stack-merge residue the
design accepts: a ternary compiling to branches pushing different types reads as unknown and
keeps full fan-out. Named test, so the residue stays a decision.

**The pop/push table is reflected out of `System.Reflection.Emit.OpCodes`**, like the operand
sizes, so the generic cases are the BCL's own arithmetic rather than a hand-written copy. Only
the instructions the ladder cares about are modelled precisely.

**The "exactly once per run" counter is on `CallGraphResult`, not a static.** A process-wide
counter read 3 when the suite ran in parallel and 1 when the file ran alone — a flaky assertion
about a real constraint is worse than none. `HierarchyConstructions` is per run and cannot race.

**Dogfooding after this ticket**: 215 of 351 test methods selected for the working tree's
changes, against 197 of 336 before. Widening is adding reach without collapsing the graph,
which is the shape the ladder exists to produce.

One assertion the ticket asks for that is worth restating because it is a *negative* result:
**an interface-typed parameter infers to the interface**, so the dependency-injection shape is
untouched by this ticket. That has its own named test, and PRD §11's main technical risk
remains unmeasured until the over-selection number exists.
