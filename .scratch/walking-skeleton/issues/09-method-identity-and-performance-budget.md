# Method identity and the performance budget

Type: grilling
Status: open
Blocked by: 05

## Question

PRD §9.4 states the constraint and the reason: "Method identity must be integers derived
from metadata tokens, never strings — the difference between seconds and minutes at
solution scale, and painful to retrofit. The type hierarchy index must be built in a
single pass; resolving implementations per call site is accidentally quadratic and will
look fine on a sample repository and fail on a client's."

**Identity.** A metadata token is unique only within one assembly, and every target
framework of a multi-targeted project produces its own assembly with its own tokens. So
identity must combine at least an assembly ordinal, a target framework, and a token.
What is the representation, how is the assembly ordinal assigned, and does it fit in a
single 64-bit value? Whatever the node definition from
[Generics, delegates and function pointers](05-generics-delegates-and-function-pointers.md)
turns out to be has to be representable in it.

**Cross-assembly resolution.** A call site in one assembly references a method in another
through a `MemberReference`, which must resolve to the `MethodDefinition` in the target
assembly to create the edge. This is the join that runs millions of times; how is it
indexed, and what happens when it fails to resolve — a reference to an assembly outside
analysis scope, a version mismatch, a type that no longer exists?

**Edge storage.** Edges carry provenance (ADR-0004) and must be walkable backwards. What
structure holds them, and is the reverse index built during the metadata pass or inverted
afterwards?

**The budget.** What does M1 actually commit to, expressed as a number against a stated
solution size? PRD §11 frames the whole tool's viability as a time comparison, so an
analysis that takes minutes has no value regardless of correctness. Decide the number, and
decide how it is measured — a benchmark that runs in CI, or a manual measurement on the
smoke target.

Also decide what is *deliberately* not optimised in M1, so the implementation tickets do
not gold-plate.
