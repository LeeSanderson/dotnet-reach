# Widening targets the inferred receiver type

Widening from a dispatch site is bounded by the **inferred receiver type** — the static
type the site can be shown to hold — rather than by the declaration named in the
instruction. Recovering it costs a single-pass abstract interpretation of the evaluation
stack, tracking static types only, inside the pass that is already decoding call
instructions. The resolution ladder is: a `newobj`/`castclass`/`isinst`/`unbox.any` operand
(an exact type), then an `ldarg`/`ldloc`/`ldsfld`/`ldfld` signature type, then the
`callvirt` token itself, then the slot-defining declaration.

Reach reads compiled assemblies, so a future reader will reasonably wonder why there is an
abstract interpreter in what should be a metadata reader. This is why.

## Why the instruction's own token is not enough

Roslyn emits `callvirt` against the **slot-defining** declaration, not the most-derived
statically-known type. Verified by disassembly: `x.ToString()` where `x` is statically a
type that *declares an override* still emits
`callvirt [System.Runtime]System.Object::ToString()`, and `using var r = new Res()` on a
**sealed** class implementing `IDisposable` emits `callvirt System.IDisposable::Dispose()`
rather than a direct call.

Taking the token at face value therefore makes fan-out maximal: every `ToString()` call
site in a solution edges to the single `object::ToString` node, which widens to every
override anywhere, so changing any one override reverse-reaches almost the entire suite.
That is not conservatism, it is a graph with no information in it.

Rung 3 of the ladder turned out stronger than expected:
`Constrained<T>(T x) where T : IShape` emits `constrained. !!T; callvirt IShape::Area()`,
so Roslyn puts the generic constraint in the token and inference gets it without reading
the constraint table.

## This is not narrowing

The glossary forbids **narrowing** — removing edges using evidence that an implementation
cannot run — because it is the only operation that can cause under-selection. Receiver-type
inference is not an instance of it. A variable of static type `Sq` cannot hold a non-`Sq`
in verifiable IL, so the bound comes from the type system rather than from inference about
runtime behaviour, and no edge that could run is removed.

## Consequences

It puts the only per-instruction work in the graph builder's hot loop, which
[Method identity and the performance
budget](../../.scratch/walking-skeleton/issues/09-method-identity-and-performance-budget.md)
has to account for. It is linear in IL size and rides a pass that already exists, but it
is not free.

Two residues, both bounded and both over-selecting rather than under-selecting. A **stack
merge** — a ternary compiling to branches that push different types — takes the common
supertype, so that one site keeps full fan-out. An **unconstrained generic receiver**
emits `constrained. !!T` with the token at `object::ToString`, and
[ADR-0006](0006-a-graph-node-is-an-il-method-definition.md) erases `T`, so it falls to the
last rung.

**It does nothing for PRD §11's main technical risk.** An interface-typed receiver —
`ViaIface(IShape s)` emitting `ldarg.0; callvirt IShape::Area()` — infers to `IShape`,
which is correct and useless. The DI shape *is* an interface-typed receiver, so
over-selection in layered DI codebases is untouched by this decision and remains
unmeasured.
