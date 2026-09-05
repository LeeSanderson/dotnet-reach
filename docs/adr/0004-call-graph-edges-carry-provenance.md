# Call graph edges carry provenance

Every edge in the call graph records why it exists — a compiled call instruction, widening
through an interface, widening through a virtual override, and later a framework model —
rather than being an undifferentiated link. The cost is a tag per edge in the hottest data
structure in the tool; it is paid from the start because retrofitting it means reworking
that structure.

## Consequences

Three things become possible that otherwise would not be. A selection can be explained
hop by hop in the report, including *why* each hop exists, which PRD §12 makes a success
criterion. The cost of widening becomes measurable — run the walk with widened edges and
without, and the difference is PRD §11's main technical risk expressed as a number on a
real solution. And any future narrowing has a safe domain to operate on, because narrowing
may only ever remove widened edges, never compiled ones.
