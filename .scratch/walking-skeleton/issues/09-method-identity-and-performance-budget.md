# Method identity and the performance budget

Type: grilling
Status: resolved
Blocked by: (none — 05 resolved)

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

## Answer

Resolved under [ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md).
The budget half is an owner decision; the representation halves take the simplest form that
satisfies PRD §9.4 and can carry [ADR-0006](../../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md)'s
node definition.

### Identity is a 64-bit value

```
bit 63        : discriminator
bits 62..32   : assembly-instance ordinal (31 bits)
bits 31..0    : metadata token, or interned external-reference id (32 bits)
```

One `readonly record struct MethodId(ulong Value)`. No strings, per PRD §9.4. It fits in 64
bits with room to spare — a metadata token is 1 byte of table id plus 3 bytes of row index,
so 32 bits is exact, and 31 bits of ordinal is four orders of magnitude more assemblies than
any solution has.

**The target framework needs no field of its own.** An *assembly instance* is one
`(project, target framework)` pair, and each gets its own ordinal — so a multi-targeted
project contributes several instances and the TFM is part of identity by construction rather
than by an extra comparison in the hottest struct in the tool. This is the single
simplification that makes the whole representation fit.

**Ordinal assignment.** First-party assembly instances are numbered from 0 in a deterministic
order — assembly simple name, then TFM moniker — taken from the solution's project list, which
[ticket 02](02-solution-and-project-file-parsing.md) already established as available without
MSBuild. External assemblies are numbered above them, in first-encounter order during a pass
that itself runs in sorted order, so the assignment is reproducible for a given input. Worth
stating because it looks like a determinism risk and is not one: **no ordinal ever reaches the
report.** Ticket 07 fixed the report on names — assembly identities as name plus TFM, roots as
kernel methods — so report determinism does not depend on ordinal stability, and ordinals are a
debugging convenience only.

**The discriminator exists because ADR-0006 needs nodes outside the analysis scope.** Widening
hangs off the dispatch *declaration* rather than being duplicated at every call site, and
ADR-0007 established that Roslyn emits `callvirt` against the slot-defining declaration — which
is routinely `object::ToString` or `System.IDisposable::Dispose`. Reach does not read those
assemblies, so there is no `MethodDefinition` token to name them by. Bit 63 set means the low
32 bits are an **interned external member reference** (keyed on assembly name plus canonical
signature) rather than a token. External nodes are widening anchors only: they are never roots,
never walked into, and no body is ever read for them. The interned set is bounded by the slots
first-party types actually implement or override, not by the BCL's size.

### Cross-assembly resolution

The distinction PRD §9.4 actually draws is that **identity** must not be strings. A one-time
resolution index keyed on strings is fine, and is the cheap answer: per target assembly
instance, one dictionary from a canonical signature string to `MethodId`, built once at load
in O(methods), then one dictionary lookup per call site. That is O(call sites), not the
accidentally-quadratic shape §9.4 warns about.

Signatures are decoded to a canonical string through a minimal `ISignatureTypeProvider` that
emits type names. It costs perhaps thirty lines and removes the whole overload-ambiguity class
that a coarser key — name plus parameter count — would leave behind.

**When resolution fails**, by case, because the cases have different correct answers:

- **Target assembly outside the analysis scope.** Normal and silent: there is no body to edge
  to. An external anchor node is interned if a first-party type implements the slot; otherwise
  nothing. This is the accepted blind spot from ADR-0008, already registered.
- **Target assembly is first-party but the member does not resolve** — a version mismatch, a
  type that no longer exists. This is a build problem, not an analysis one, so it is a
  **notice** and the run continues; ADR-0003's correspondence check is the mechanism that
  should have caught it earlier.
- **Residual ambiguity** — varargs, `modopt`/`modreq`, function-pointer parameters — **edges to
  every candidate.** Widening is the safe direction and the case is rare enough not to pay for
  exactness.

### Edge storage

Edges accumulate during the metadata pass as a flat growable array of
`(from: MethodId, to: MethodId, provenance: byte)`. After the pass, sort by `to` once and
build a CSR reverse adjacency — an offsets array plus a targets array. **The reverse index is
inverted afterwards, not built during the pass**, because the node count is not known until
the pass ends, and inverting once is cheaper than maintaining per-node lists while appending.

**Only the reverse index is built.** Reverse reachability walks backwards; ticket 07's forward
change list carries counts derived from the walk's roots rather than from a forward index; and
opt-in hop-by-hop paths reconstruct backwards, which the reverse index already serves. A
forward index would be a second structure obliged to agree with the first.

### The budget: no committed number in M1

**Owner decision.** M1 commits to no wall-clock figure against a stated solution size. There
is no measurement yet, so any number written now would be invented, and an invented number in
a spec becomes a gate someone later fails for no reason.

What M1 commits to instead is checkable:

1. **The two PRD §9.4 algorithmic constraints, asserted by tests rather than by review.**
   `sizeof(MethodId) == 8` and no string-typed field in the node or edge structures; and the
   type hierarchy index built exactly once per run, asserted with an instrumented construction
   counter over a fixture.
2. **The report's phase timings, always emitted.** Ticket 07 already put them in the envelope
   for other reasons, so the instrument exists at zero extra cost.
3. **The first real run is the first datapoint**, and the number gets set from evidence. This
   is consistent with the owner's waiver of PRD §11's M0 measurement: the ratio and the budget
   are both deferred until something can actually be measured.

The scaling *shape* is what §9.4 cares about and it is what the assertions defend. A test that
doubles a fixture and asserts sub-quadratic growth would be flaky in CI and is not adopted.

### Deliberately not optimised in M1

So implementation tickets do not gold-plate, and so a reviewer does not read these as
oversights:

- **No concurrency of any kind.** Graph construction is single-threaded. This closes the map's
  open fog on parallelism: M1 commits to none. Assembly reads are the obvious parallel seam and
  are left for when there is a number to improve.
- **No incremental or persisted graph** — already settled by PRD §9.3 and
  [ADR-0001](../../../docs/adr/0001-reach-persists-no-state-between-runs.md).
- No memory-mapped assembly reads, no object pooling or custom allocators, no
  struct-of-arrays node layout, and no string interning beyond the resolution index — which is
  rebuilt every run.

### Consequences for other tickets

- **[Project layout and ports](10-project-layout-and-ports.md)** is unblocked. The core takes
  already-opened metadata readers, which is what the in-memory-compiled test story needs.
- **[Write the spec](12-write-the-spec.md)** inherits the identity layout, the three
  resolution-failure cases and the not-optimised list. The join stays abstract regardless: this
  ticket fixes the identity a join must *produce*, not how a changed declaration reaches it.
- **[The limitations register](19-the-limitations-register.md)** gains the ambiguous-signature
  widening as a named, deliberate over-selection.
