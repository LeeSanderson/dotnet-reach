# The limitations register

Type: grilling
Status: open
Blocked by: (none — 07 resolved, see Comments)

## Question

Graduated from the fog by [Generics, delegates and function
pointers](05-generics-delegates-and-function-pointers.md), which accepted a set of named
under-selection holes rather than widening for each of them
([ADR-0008](../../../docs/adr/0008-m1-accepts-named-under-selection.md)).

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
  [ADR-0005](../../../docs/adr/0005-change-detection-is-keyed-on-declared-type.md).

The output is the register's format and its first full contents, in a form
[Write the spec](12-write-the-spec.md) can reference rather than restate.

## Comments

**From [The report contract](07-the-report-contract.md):** that ticket has resolved, and it
answers two of the questions above while adding one requirement.

**"Where does it live?" is partly settled.** The report emits **notices**, each identified by
a stable kebab-case code that is never renamed, carrying one `kind` from `blind-spot`,
`widening`, `scope`, `environment`. The register is therefore the *documentation* of those
codes, keyed by code — which is why slugs were chosen over numeric IDs, since a slug is a
natural anchor and a number needs a lookup table. This ticket still owns the register's own
format and whether it is generated, but the join between register and report is fixed: the
code.

**The requirement**: **every `blind-spot` code must have a register entry, and Reach's own
test suite asserts the two sets match exactly.** That converts ADR-0008's "named and
surfaced" clause from a promise into a build failure. It also answers the register's own
question about detectability from the other direction — a hole Reach can detect *has* a code
and must be in the register; a hole it cannot detect has no code, and this ticket decides
how it is documented instead.

**"What does the report say at runtime?" — the noisy-warning problem is real, and M1's answer
is to live with it.** Notice suppression is deliberately **not in M1**: a suppression switch
is a mechanism for un-surfacing exactly the holes the ADR-0008 bargain depends on surfacing,
and there is no evidence yet about which codes are actually noisy in practice. Worth writing
into the register now, while the reasoning is fresh: if suppression ever lands, `blind-spot`
must be non-suppressible, or the bargain is void.

Two seed entries the report contract adds: a change routed at a coarse tier whose widening
was **downgraded from a filter to whole-project selection** because the caller's runsettings
carried a `<TestCaseFilter>` (ADR-0009), and the **dialect over-match** from rendering NUnit
with `~`, which is an over-selection rather than a hole but belongs in the same catalogue
since it is a deliberate, named imprecision.
