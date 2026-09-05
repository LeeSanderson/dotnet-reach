# Generics, delegates and function pointers in the graph

Type: grilling
Status: open
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
