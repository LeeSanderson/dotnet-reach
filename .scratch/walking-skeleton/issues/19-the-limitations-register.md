# The limitations register

Type: grilling
Status: open
Blocked by: (none — informed by 07)

## Question

Graduated from the fog by [Generics, delegates and function
pointers](05-generics-delegates-and-function-pointers.md), which accepted a set of named
under-selection holes rather than widening for each of them
([ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md)).

That decision is only safe while the holes are visible to whoever is deciding whether to
trust a selection. PRD §10's claim is conservatism **plus disclosure** — "it says so when
it cannot see" — so the register is what keeps M1 honest, not a documentation chore. This
ticket decides its shape.

**Where does it live?** One document, a section of the report schema, or both with one
generated from the other? A list that only exists in prose drifts from the code the first
time a hole is closed; a list that only exists in the report cannot be read by someone
deciding whether to adopt Reach at all.

**What is a register entry?** At minimum, the shape of the hole and its direction of
failure. Candidates for the rest: whether Reach can *detect* an instance of it in a given
run, the known upgrade path, and which milestone is expected to close it. An entry Reach
can detect is a different product surface from one it cannot — the first can be reported
per run, the second can only ever be documentation.

**What does the report say at runtime?** This is where it meets
[The report contract](07-the-report-contract.md). A run that hit a detectable limitation
should say so, but a run that merely *could* have hit an undetectable one is every run, and
a warning printed every time is a warning nobody reads. Decide which entries surface per
run, which surface only under a flag, and which never surface.

**Does an accepted hole need an owner or a trigger?** ADR-0005's precedent is a recorded
trigger for revisiting a decision. The equivalent here would be a condition under which a
hole stops being acceptable — a shadow-mode result, a user report, a framework model
landing.

**Seed content**, from ticket 05 and the resolved tickets before it:

- First-party members invoked only from outside the analysis scope — interpolation calling
  `ToString`, `Equals`/`GetHashCode` via a dictionary, `CompareTo` via `Sort`, and every
  framework base-class override a host invokes. Undetectable in general; the whole-type
  widening rule in ADR-0008 is the named upgrade path.
- Function pointers obtained without an `ldftn` in analysed IL —
  `Delegate.CreateDelegate`, `Marshal.GetFunctionPointerForDelegate`, native interop.
  Partly detectable: a `calli` with no inferable source is visible.
- Deserialization and reflective construction.
- Ignored-and-untracked compiled files, from [Change-set edge
  cases](04-change-set-edge-cases.md) — already established as detectable but not
  selectable-on.
- The deferred absorption test for deleted methods, from the same ticket.
- Renames are never detected —
  [ADR-0005](../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md).

The output is the register's format and its first full contents, in a form
[Write the spec](12-write-the-spec.md) can reference rather than restate.
