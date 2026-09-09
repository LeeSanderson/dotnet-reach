# A graph node is an IL method definition

A node in the call graph is one `MethodDefinition`, per assembly, per target framework —
never a source declaration and never a generic instantiation. A generic definition and
every instantiation of it collapse to one node, so the type arguments at a call site are
not part of identity. Accessors are nodes in their own right, because `get_Total` and
`op_Equality` are ordinary methods in IL. Dispatch declarations are nodes too: a
`callvirt IRepo.Save` edges to a node for `IRepo.Save`, and the widening edges hang off
*that* node rather than being duplicated at every call site.

## Considered options

**One node per generic instantiation** is more precise — a change reachable only through
`Handler<Foo>` would stop selecting tests that use only `Handler<Bar>`. Rejected as
unbounded in principle, needing a rule for instantiations closed over type parameters, and
because the collapsed form is the conservative direction. The type argument stays
*readable* at the call site (`MethodSpec` carries it), so a later framework model can use
it without this decision being reversed.

**A synthetic node per property or event**, with its accessors folded in, would match how
a developer describes a change. Rejected: it makes the graph invent a member IL does not
have, and reintroduces the string-ish identity PRD §9.4 forbids. The fan-out from a
changed property to its accessors belongs in the **join**, which is already the seam for
translating source declarations into graph identities, and the report renders the
developer-facing name so nothing is lost in explainability.

**Call sites edging directly to every implementation**, with no node for the interface or
base declaration, would save a hop. Rejected because it smears every widened edge across
the graph, where it can neither be isolated nor counted — which would break the widening
measurement [ADR-0004](0004-call-graph-edges-carry-provenance.md) exists to enable.

## Consequences

Identity must combine an assembly ordinal, a target framework and a metadata token, since
a token is unique only within one assembly and a multi-targeted project produces one
assembly per framework. The representation is
[Method identity and the performance
budget](../../.scratch/walking-skeleton/issues/09-method-identity-and-performance-budget.md).

`MethodImpl` is authoritative for mapping an interface member to its implementation,
because an explicit interface implementation is deliberately not name-matchable; name and
signature matching is only the fallback for implicit implementations.

Static abstract interface members widen to every implementer. The implementation is
selected by the generic instantiation at the call site — precisely what this decision
discards — so there is nothing narrower available, and the widening direction is the safe
one. It is the most visible cost of collapsing instantiations.

This contradicts PRD §9.2's claim that IL "exposes … generic instantiations as concrete
methods". It exposes the instantiation at the *call site*, not as a distinct definition.
