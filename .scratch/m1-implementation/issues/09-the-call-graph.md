# The call graph: identity, the metadata pass and compiled edges

Status: resolved
Depends on: 07
Spec: [§11.1](../../walking-skeleton/spec.md#111-a-node-is-an-il-method-definition)–[§11.4](../../walking-skeleton/spec.md#114-the-seven-edge-kinds), [§11.6](../../walking-skeleton/spec.md#116-the-address-taken-rule) · [ADR-0004](../../../docs/adr/0004-call-graph-edges-carry-provenance.md), [ADR-0006](../../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md)

## Goal

One pass over the analysis scope's assemblies producing nodes, **compiled** edges and a reverse
index. Widening and synthesised edges come later; this ticket is the structure they hang off.

## Scope

**A node is one `MethodDefinition`, per assembly instance.** Never a source declaration, never a
generic instantiation.

- **Generic instantiations collapse** to one node per definition. Cost accepted and registered:
  a change reachable only through `Handler<Foo>` also selects tests using only `Handler<Bar>`.
  The type argument stays *readable* at the call site (`MethodSpec` carries it), so a later
  framework model can use it without reversing this.
- **Accessors are nodes in their own right** — `get_Total`, `op_Equality`, `add_Changed` are
  ordinary methods in IL. Do **not** invent a synthetic property or event node: it would make the
  graph carry a member IL does not have and reintroduce string-ish identity. The fan-out from a
  changed *declaration* belongs in the join (ticket 10).
- **Dispatch declarations are nodes.** `callvirt IRepo.Save` edges to a node for `IRepo.Save`,
  and ticket 14's widening edges hang off *that* node. Do not edge call sites straight to
  implementations: it smears widening across the graph where it can be neither isolated nor
  counted, which would break the measurement ADR-0004 exists to enable.

**Identity is a 64-bit value.** One `readonly record struct MethodId(ulong Value)`:

```
bit 63        : discriminator
bits 62..32   : assembly-instance ordinal (31 bits)
bits 31..0    : metadata token, or interned external-reference id (32 bits)
```

No strings, per PRD §9.4. A metadata token is 1 byte of table id plus 3 bytes of row index, so
32 bits is exact; 31 bits of ordinal is four orders of magnitude more assemblies than any
solution has.

**The target framework needs no field of its own** — an assembly instance *is* one
`(project, target framework)` pair and each gets its own ordinal, so multi-targeting falls out of
the ordinal and the hottest struct in the tool stays 8 bytes. This is the single simplification
that makes the representation fit; do not add a TFM field.

**Ordinals** are assigned to first-party instances from 0 in a deterministic order — assembly
simple name, then TFM moniker — from ticket 04's project list, and to external assemblies above
them in first-encounter order during a pass that itself runs in sorted order. **No ordinal ever
reaches the report**, so report determinism does not depend on ordinal stability; they are a
debugging convenience.

**The discriminator exists because widening needs anchors Reach never reads.** Roslyn emits
`callvirt` against the slot-defining declaration, which is routinely `object::ToString` or
`System.IDisposable::Dispose`, and those assemblies are not in the analysis scope, so there is no
`MethodDefinition` token to name them by. Bit 63 set means the low 32 bits are an **interned
external member reference**, keyed on assembly name plus canonical signature. External nodes are
widening anchors only: never roots, never walked into, **no body ever read**. The interned set is
bounded by the slots first-party types actually implement or override, not by the BCL's size.

**Cross-assembly resolution.** Per target assembly instance, one dictionary from a canonical
signature string to `MethodId`, built once at load in O(methods), then one lookup per call site.
That is O(call sites), not the accidentally-quadratic shape PRD §9.4 warns about. §9.4 forbids
strings in *identity*, not in a build-once lookup. Signatures are decoded through a minimal
`ISignatureTypeProvider` emitting type names — perhaps thirty lines, and it removes the whole
overload-ambiguity class a name-plus-parameter-count key would leave behind.

**Resolution failure has three different right answers, and they must not be collapsed:**

| Case | Response |
|---|---|
| target assembly outside the analysis scope | normal and **silent**; intern an external anchor if a first-party type implements the slot, else nothing. The accepted blind spot, already registered |
| target assembly is first-party but the member does not resolve | **notice**, run continues — a build problem, not an analysis one; ticket 08 is what should have caught it |
| residual ambiguity (varargs, `modopt`/`modreq`, function-pointer parameters) | **edge to every candidate**, `signature-ambiguous` notice. Widening is the safe direction |

**Compiled edges in this ticket**, and only these two kinds:

- `call` / `callvirt` to an exactly-resolved target — provenance **compiled call**;
- `ldftn` / `ldvirtftn` capture — provenance **compiled capture**, from the **capturing** method
  to the target.

**`Invoke` and `calli` create no edges.** Edging every `Invoke` on a delegate type to every method
ever captured into it looks like widen-when-uncertain and is not conservatism: `Func<T>` and
`Action` are structural types shared by unrelated code, so it connects everything to everything
and makes the graph useless rather than safe. The capture-site rule is far less lossy than it
looks, because a test that can execute the target must have executed the code that created the
delegate. `delegate*` values fall out for free — they are `ldftn` like any other capture. A
pointer obtained without a visible `ldftn` is a registered blind spot with partial detection
(`calli-unresolved`).

**Edge storage.** Accumulate during the pass as a flat growable array of
`(from: MethodId, to: MethodId, provenance: byte)`. After the pass, sort by `to` once and build a
**CSR reverse adjacency** — an offsets array plus a targets array.

**The reverse index is inverted afterwards, not built during the pass**: the node count is not
known until the pass ends, and inverting once is cheaper than maintaining per-node lists while
appending. **Only the reverse index is built** — a forward index would be a second structure
obliged to agree with the first, and nothing needs it: the walk goes backwards, the forward change
list carries counts derived from the walk's roots, and opt-in hop-by-hop paths reconstruct
backwards.

**Provenance is a tag per edge in the hottest structure in the tool, and it is paid from the
start** because retrofitting it means reworking that structure. Three classes — compiled,
synthesised, widened — of which this ticket produces only the first.

**The core takes already-opened `PEReader`/`MetadataReader` instances as parameters.** There is no
`IAssemblyReader` interface: the BCL type already *is* the seam, so an in-memory Roslyn
compilation emitted to a `MemoryStream` and a file on disk are the same type. Wrapping it would be
a shallow module that fails the deletion test.

**Deliberately not optimised**, so this ticket does not gold-plate and a reviewer does not read
these as oversights: **no concurrency of any kind** — graph construction is single-threaded, and
assembly reads are the obvious parallel seam left for when there is a number to improve; no
incremental or persisted graph; no memory-mapped reads; no object pooling or custom allocators; no
struct-of-arrays node layout; no string interning beyond the resolution index, which is rebuilt
every run.

## Acceptance criteria

- **`sizeof(MethodId) == 8`**, asserted by a test, and no string-typed field in the node or edge
  structures — also asserted rather than reviewed. These are two of the three things M1's
  performance budget actually commits to.
- An in-memory compilation with a plain `call` produces one compiled edge with the right
  provenance.
- `ldftn` produces an edge from the **capturing** method; `Invoke` produces none.
- A generic method used at two instantiations produces **one** node.
- An accessor is a node; no synthetic property node exists.
- A `callvirt` against an interface produces a node for the interface member.
- A call into an assembly outside the analysis scope produces no edge and no notice, and interns
  an external anchor only when a first-party type implements the slot.
- An unresolvable first-party member produces a notice and does not stop the run.
- An ambiguous signature edges to every candidate and emits `signature-ambiguous`.
- The reverse index round-trips: for a known graph, every edge is reachable backwards exactly
  once.
- Phase timings for the pass are emitted.

## Out of scope

Widening (ticket 14), containment and type-initializer edges (ticket 15), the walk (ticket 11),
the join (ticket 10).

## Comments

**Implemented** in `Reach.Core/Graph`: `MethodId`, `EdgeProvenance`, `ILInstructions`,
`SignatureNames`/`MetadataNames`, `GraphAssembly`, `ExternalAnchors`, `CallGraphBuilder`,
`CallGraph` and `OpenAssemblies`.

**The IL operand-size table is built by reflecting over `System.Reflection.Emit.OpCodes`, not
typed out.** The BCL owns that table; a hand-written copy is about a hundred and eighty chances
to be wrong in a way no test would obviously catch, and a mis-decoded body is a missed edge,
which is under-selection. An unrecognised opcode stops the walk for that body rather than
guessing at a length and reading operand bytes as instructions.

**Every token-carrying instruction is recorded, not just the calls.** `ILInstructions.Tokens`
returns offsets and opcodes for all of them, so ticket 15 can read the field and type tokens
for type-initializer edges and ticket 14 can see the `constrained.` prefix, without a second
pass over the IL.

**Signature ambiguity has a real fixture.** `modopt`/`modreq` and varargs are hard to produce
from C#, but two function-pointer parameter types differing only by calling convention —
`delegate*<int>` and `delegate* unmanaged<int>` — are distinct types to the compiler and
identical once canonicalised. The test asserts both halves: an edge to each candidate, and the
`signature-ambiguous` notice. `Compiled` now sets `allowUnsafe`.

**`SignatureNames` captures the first type handle it reaches.** A member reference against a
generic type instance names its parent by a `TypeSpecification`, which carries no assembly of
its own, so that capture is how the assembly behind one is recovered. Without it, every call
into a generic type in another first-party assembly would resolve to nothing.

**A member reference against a generic instance is looked up under the generic definition's
name**, because that is the signature metadata actually carries — `List\`1<System.Int32>::Add`
is stored as `Add(!0)`. `MetadataNames.WithoutInstantiation` cuts at the first `<`, which is
also correct for a nested type under a generic instance.

**Two ordinal rules that fell out of the design rather than being added to it:**

- A multi-targeted project is several instances with several ordinals, and a reference resolves
  to the instance whose target framework matches the *caller's* — a `net8.0` assembly
  references the `net8.0` build. `TargetFor` does that, falling back to the first candidate.
- `System.Object` is a base type of everything, so its slots always anchor. That is four
  anchors at most, and is exactly what "bounded by the slots first-party types occupy" means.
  The first version of one test assumed a single anchor and was wrong.

**`OpenAssemblies` prefetches the entire image.** `PrefetchMetadata` leaves method bodies in
the file, so `GetMethodBody` needs the stream alive for the whole run — which means a file
handle held open on every assembly in the solution. Three tests failed on exactly that before
it was changed.

**The unresolved-first-party test compiles a consumer against one revision of a library and
puts a different revision in the graph**, which is what a stale assembly on disk looks like
from here. `Graphs.OfMismatched` does it in three lines.

Phase timing is on `CallGraphResult.Elapsed`. It reaches the report's timings envelope with
ticket 12.
