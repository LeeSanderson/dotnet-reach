# Call graph edges carry provenance

Every edge in the call graph records why it exists — a compiled call instruction, widening
through an interface, widening through a virtual override, and later a framework model —
rather than being an undifferentiated link. The cost is a tag per edge in the hottest data
structure in the tool; it is paid from the start because retrofitting it means reworking
that structure.

## Amendment: provenance has three safety classes, not two

Recorded when [Generics, delegates and function
pointers](../../.scratch/walking-skeleton/issues/05-generics-delegates-and-function-pointers.md)
enumerated the edge kinds and found seven, not four. Two of them — **containment** (a
kernel method to the compiler-generated members carrying its body) and **type
initialization** (an initialization trigger to a `.cctor`) — are synthesised by Reach
because no instruction expresses them, yet the control flow they describe is not in doubt.

They fit neither half of this ADR's original two-way split, so the classes are:

- **Compiled** — read from an instruction. A `call`/`callvirt` to an exactly-resolved
  target, and an `ldftn`/`ldvirtftn` capture.
- **Synthesised** — invented, but certain. Containment and type initialization.
- **Widened** — speculative. Interface and virtual-override widening, and `ldvirtftn`
  fan-out.

Two consequences of the amendment. The rule below becomes "narrowing may only ever remove
**widened** edges" — synthesised edges are as non-removable as compiled ones, which the
original wording did not cover. And the measurement below sharpens: "the walk with widened
edges and without" means the compiled and synthesised kinds versus all of them.

## Consequences

Three things become possible that otherwise would not be. A selection can be explained
hop by hop in the report, including *why* each hop exists, which PRD §12 makes a success
criterion. The cost of widening becomes measurable — run the walk with widened edges and
without, and the difference is PRD §11's main technical risk expressed as a number on a
real solution. And any future narrowing has a safe domain to operate on, because narrowing
may only ever remove widened edges, never compiled ones.
