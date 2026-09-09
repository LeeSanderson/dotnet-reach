# Generics, delegates and function pointers in the graph

Type: grilling
Status: resolved
Blocked by: (none)

## Question

PRD §9.2 claims IL "exposes async state machines, lambdas and generic instantiations as
concrete methods". That is true of the first two and only partly true of the third, and
the difference decides what a node in the call graph actually is.

**Generics.** A generic method definition and its instantiations share one metadata
definition; call sites reference a `MethodSpec`. Does the graph have one node per generic
definition, or one per instantiation? One node per definition is smaller and simpler and
over-selects — a change to code only reachable through `Handler<Foo>` also selects tests
that only use `Handler<Bar>`. One node per instantiation is more precise, unbounded in
principle, and needs a rule for instantiations closed over type parameters.

**Delegates.** `ldftn` and `ldvirtftn` capture a method reference into a delegate;
`Invoke` on the delegate calls it indirectly, and the two are connected only through data
flow the graph does not model. What edge does Reach create — from the capturing method
straight to the target, from every `Invoke` to every method ever captured into that
delegate type, or something narrower? Events, `Func`/`Action` parameters and LINQ all
travel this path, so getting it wrong is not an edge case.

**Function pointers.** `calli` and `delegate*` have no callee in metadata at all. Presumably
a blind spot; confirm and decide what the report says about them.

**Also settle:** explicit interface implementations; default interface methods; static
abstract interface members, where the implementation is selected by generic instantiation
at the call site; and property, indexer and operator accessors, which are ordinary methods
in IL but not in how a developer describes a change.

The output of this ticket is a definition of what a **node** is and which edge kinds exist
— which is what [Method identity and the performance budget](09-method-identity-and-performance-budget.md)
then needs to represent.

## Answer

Every IL claim below was verified by disassembling a purpose-built subject assembly
(`net8.0`, Release) with `ilspycmd -il`, not from memory. Two of them overturned the
answer that was about to be recorded. The subject is kept at
[assets/il-subject](../assets/il-subject/README.md), which maps each claim to the type that
demonstrates it.

### A node is an IL method definition

One node per `MethodDefinition`, per assembly, per target framework. Not per
instantiation, not per source declaration.

**Generics.** One node per generic *definition*; the type arguments at a call site are not
part of identity. Per-instantiation nodes are more precise and unbounded in principle, and
need a rule for instantiations closed over type parameters. Cost accepted: a change
reachable only through `Handler<Foo>` also selects tests that only use `Handler<Bar>`.
The type argument is still *readable* at the call site — `MethodSpec` carries it — it is
just not part of identity, which leaves the door open for a later model to use it.

**Accessors.** `get_Total`, `set_Total`, `op_Equality`, `add_Changed` are ordinary methods
in IL, and the node is the accessor. The **join** fans a changed property, indexer, event
or operator declaration out to its accessors; the report renders the developer-facing name
so a selection stays explainable. Folding accessors into a synthetic property node would
make the graph invent a member IL does not have and reintroduce string-ish identity that
PRD §9.4 forbids. Two riders: an auto-property whose *initializer* changes is a change to
the constructor, not to an accessor; a field-like event's generated `add`/`remove` fan out
the same way.

**Dispatch declarations are nodes.** `callvirt IRepo.Save` edges to a node for
`IRepo.Save` itself, and widening edges run from *that* node to the implementations. The
alternative — every call site edging straight to every implementation — smears the
widening across the whole graph, where it can be neither isolated nor counted, which
would break the measurement ADR-0004 already committed to. The `MethodImpl` table is
authoritative for the interface-to-implementation mapping, because an explicit interface
implementation is not name-matchable; name and signature matching is the fallback for
implicit implementations. A default interface method is a real body on the interface, so
widening from an interface node reaches the DIM body *alongside* every override.

### Compiler-generated members belong to their kernel method

PRD §9.2 says IL "exposes async state machines, lambdas and generic instantiations as
concrete methods". True, and it hides a correctness hole. Verified: `async Task<int> Aw()`
reaches its own body only through
`AsyncTaskMethodBuilder<int>::Start<'<Aw>d__0'>` — `MoveNext` is invoked from inside the
BCL. So a walk backwards from a change inside *any* `async` method body dead-ends in an
assembly that is not first-party and is not analysed. That is under-selection on ordinary
modern C#, not an edge case.

The fix is exact rather than heuristic. Verified present on the kernel methods:
`[AsyncStateMachine(typeof('<Aw>d__0'))]` and
`[IteratorStateMachineAttribute(typeof('<It>d__1'))]`, each naming the generated type
directly. A **containment edge** runs from the kernel method to the generated members, so
a change inside them resolves to the method the developer actually wrote. The
name-mangling convention (`<Foo>b__0`, `<Foo>g__Local|0_1`, `<>c__DisplayClass`) is the
fallback for closures the attributes do not cover.

Also verified, and reassuring: lambdas need no special rule — `Lam()` emits
`ldftn '<>c'::'<Lam>b__2_0'` inside the kernel method, so the address-taken rule below
already covers them. Local functions are an ordinary `call`. Iterators differ from async
in shape — `It()` does `newobj '<It>d__1'::.ctor`, a real compiled edge — but `MoveNext`
still arrives via interface dispatch from the consumer's `foreach`, so containment carries
it either way.

### The address-taken rule

`ldftn`/`ldvirtftn` name their target in metadata; `Invoke` and `calli` name nothing.

An edge runs from the **capturing** method to the target at `ldftn`. `Invoke` and `calli`
create no edges. `ldvirtftn` names a virtual method, so it also produces the widened
edges to the overrides.

Edging every `Invoke` on a delegate type to every method ever captured into that type
looks like the widen-when-uncertain rule but is not conservatism: `Func<T>` and `Action`
are structural types shared by unrelated code, so it connects everything to everything and
makes the graph useless rather than safe. The capture-site rule is far less lossy than it
looks, because a test that can execute the target must have executed the code that created
the delegate.

**Function pointers** fall out for free: `delegate*` values are `ldftn` like any other
capture. The residue is a pointer obtained without an `ldftn` Reach can see —
`Delegate.CreateDelegate`, `Marshal.GetFunctionPointerForDelegate`, native interop. This
is the one place where the correctness rule does *not* force widening, because every
method whose address is taken in analysed code already has its capture-site edge, so the
only widening available is unbounded. Recorded as a blind spot instead.

### Type initializers are an edge kind

An edge is synthesised from every method that triggers initialization (`newobj`, static
field or method access on the type) to that type's `.cctor`. It is cheap — the trigger
instructions are already being decoded to find call edges — and it closes a hole the
address-taken rule would otherwise open. Verified as a real case, not a hypothetical: a
`static readonly Func<int,int>` field compiles to `ldftn` **inside `.cctor`**, which
nothing visibly calls, so without this edge the captured lambda is orphaned.

### Widening targets the inferred receiver type

The assumption this ticket nearly recorded — that Roslyn emits `callvirt` against the
most-derived statically-known type — is **false**. Verified:
`OnOverrider(Overrider x) => x.ToString()`, where `Overrider` declares an override, still
emits `callvirt [System.Runtime]System.Object::ToString()`. `using var r = new Res()` on a
**sealed** class implementing `IDisposable` emits `callvirt System.IDisposable::Dispose()`
rather than a direct call. Roslyn emits the slot-defining declaration.

Taken alone that makes fan-out maximal: every `ToString()` call site in a solution edges
to the single `object::ToString` node, which widens to every override anywhere, so
changing any one override reverse-reaches almost the whole suite.

It is not taken alone. The receiver's static type is recoverable from the instructions
that produced it, by a single-pass abstract interpretation of the evaluation stack
tracking static types only. Resolution ladder, in order:

1. `newobj` / `castclass` / `isinst` / `unbox.any` operand — an *exact* type
2. `ldarg` / `ldloc` / `ldsfld` / `ldfld` signature type
3. the `callvirt` token itself
4. the slot-defining declaration

Verified: `ViaField` emits `ldsfld class Sq Inf::_f` before the callvirt, giving `Sq`;
Release codegen collapsed `ViaLocal` to `newobj Sq::.ctor; callvirt object::ToString()`, an
exact type. Rung 3 is stronger than expected — `Constrained<T>(T x) where T : IShape`
emits `constrained. !!T; callvirt IShape::Area()`, so **Roslyn puts the generic constraint
in the token**, and inference gets it without reading the constraint table.

This is not **narrowing** under the glossary's definition: a variable of static type `Sq`
cannot hold a non-`Sq` in verifiable IL, so no edge that could run is removed.

Two residues, both bounded. A **stack merge** — verified in `Merge`, where a ternary
compiled to branches pushing `Sq` on one path and `Plain` on the other — takes the common
supertype, here `object`, so that one site keeps full fan-out. An **unconstrained generic
receiver** emits `constrained. !!T` with the token at `object::ToString`, and identity
erases `T`, so it falls to rung 4.

What inference does *not* buy: `ViaIface(IShape s)` emits `ldarg.0; callvirt
IShape::Area()`, so inference returns `IShape` — correct and useless. The DI shape *is* an
interface-typed receiver, so **PRD §11's main technical risk is untouched by this.**

### The seven edge kinds, and what narrowing may touch

| Edge | Provenance | Narrowable? |
| --- | --- | --- |
| `call`/`callvirt` to an exactly-resolved target | compiled call | never |
| `ldftn`/`ldvirtftn` capture | compiled capture | never |
| widening through an interface | widened | yes |
| widening through a virtual override | widened | yes |
| `ldvirtftn` fan-out to overrides | widened | yes |
| kernel method → compiler-generated member | containment | never |
| initialization trigger → `.cctor` | type initialization | never |

The split that matters is three-way, not two. **Compiled** edges are read from an
instruction. **Synthesised** edges — containment and type initialization — were invented
by Reach, but the control flow is not in doubt, so they are as non-removable as compiled
ones. **Widened** edges are the only speculative class, and therefore the only class any
future narrowing may touch.

ADR-0004 promised narrowing "may only ever remove widened edges, never compiled ones",
written when only two classes existed; containment and type initialization are neither.
[ADR-0004](../../docs/adr/0004-call-graph-edges-carry-provenance.md) has been amended
rather than left quietly wrong. This also sharpens its measurement: "the walk with
widening and without" means the four non-widened kinds versus all seven.

### What M1 does not see

Scope call by the owner, mid-ticket: stop resolving every edge case, document the
constraints, and let coverage grow. This is a proof of concept, and working code with
stated limits beats an exhaustively-specified one that does not exist.

This **overrides the map's standing note that the correctness rule outranks everything**,
for this ticket's residues only, and it is recorded in
[ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md) rather than left
implicit. It is defensible because PRD §10 already scopes the product claim to
"conservative, and that it says so when it cannot see" — but *only* while the holes are
named somewhere a user actually reads. That makes the constraints list load-bearing, and
it is now [The limitations register](19-the-limitations-register.md).

The known holes at the close of this ticket:

- **First-party members invoked only from outside the analysis scope.** Verified:
  `$"{n}-{o}"` emits `DefaultInterpolatedStringHandler::AppendFormatted<class Overrider>`
  and never names `ToString` at all, so a test exercising an override only through
  interpolation cannot reach it. Generalises well past `ToString` — `Equals`/`GetHashCode`
  via a dictionary, `CompareTo` via `Sort`, and every framework base-class override
  (`BackgroundService.ExecuteAsync`, `DbContext.OnModelCreating`,
  `Controller.OnActionExecuting`) that the host calls and first-party code never does.
  Receiver-type inference cannot help: there is no call site to infer from. This is what
  **framework models** are for (PRD §6), and M1 ships none.
- **Function pointers from outside analysed IL** — `Delegate.CreateDelegate`, native
  interop, `Marshal.GetFunctionPointerForDelegate`.
- **Deserialization and reflective construction**, which defeat any argument that leans on
  the constructor being visible in first-party IL.
- **Unconstrained generic receivers**, which fall to the slot-defining declaration and so
  over-select rather than under-select — a cost, not a hole.

A **whole-type widening** rule was designed for the first of these and *not* adopted under
the scope call: widen the declaring type whenever a changed member overrides or implements
a contract declared outside the analysis scope, leaning on the constructor as a choke point
(the object had to be constructed by first-party code, and `.ctor` is a member of the
type, so the widening reaches any test that constructs it even if the test only ever holds
it as `object`). It is written down here because it is the cheapest known upgrade path —
it needs the base-type walk and `MethodImpl` table that Q1's widening already builds — and
because M2's models supersede it. Recorded on
[The limitations register](19-the-limitations-register.md) as the named next step, so the
hole has an owner rather than being forgotten.

### Consequences for other tickets

- **[Method identity and the performance budget](09-method-identity-and-performance-budget.md)**
  is unblocked, and inherits two things: identity is per method definition, per assembly,
  per target framework, with generic instantiations collapsed; and the graph builder now
  carries a per-instruction stack simulation, the only per-instruction work in the hot
  loop, which the budget has to account for.
- **[The report contract](07-the-report-contract.md)** inherits the provenance taxonomy as
  a schema obligation, and the question of which limitations the report names at runtime
  versus which live only in documentation.
- **[Fixture catalogue](11-fixture-catalogue.md)** is unblocked. Every IL shape verified
  here is a fixture candidate, and [assets/il-subject](../assets/il-subject/README.md) is a
  starting point: async, iterators, lambdas, `static readonly` delegate fields, local
  functions,
  explicit interface implementations, DIMs, static abstract members, accessors, sealed-type
  `using`, interpolation, unconstrained and constrained generic receivers, and a stack
  merge.
- **[PRD amendments](06-prd-amendments.md)** gains §9.2, whose claim about generic
  instantiations and async state machines is the origin of two of this ticket's holes.
- **[Defining the over-selection measurement](17-defining-the-over-selection-measurement.md)**
  gains receiver-type inference as the main lever on fan-out, worth measuring with and
  without.

### Static abstract interface members

Widen from the static abstract declaration to every implementer, as with ordinary
interface widening. The implementation is selected by the generic instantiation at the
call site — exactly the information identity discards — so this is the honest consequence
of one node per generic definition, and the widening direction is the safe one. Flagged as
a visible cost of that decision in case the over-selection measurement later argues for
revisiting it.
