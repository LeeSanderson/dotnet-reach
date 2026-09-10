# What counts as a changed member, and cross-assembly recompilation effects

Type: grilling
Status: resolved
Blocked by: (none)

## Question

Surfaced by
[Change-set edge cases](04-change-set-edge-cases.md), which found the hole while deciding
what a deleted method resolves to and deliberately left it here.

Hashing **source** member bodies cannot see a change that alters how an **unchanged** body
compiles. The compiler bakes values and binding decisions into consumers, so the affected
code's source is byte-identical while its IL is different — and nothing puts it in the
changed set. Every variant under-selects.

**The unit of change detection.** Ticket 04 settled that the changed set is computed by
matching declared types between revisions (ADR-0005), but not what is compared *within* a
matched type. A `const` value, a field initializer, an attribute argument and an enum
member are not method bodies, so a body-level hash never sees them. A field initializer is
at least compiled into the constructor, so treating the unit as the **member declaration**
rather than the body may cover most of this — but that has to be decided, not assumed.

**Compile-time constants.** A changed `const`, enum member or default parameter value is
inlined into every consumer's IL, across assembly boundaries. The declaring assembly's own
widening never reaches them.

**Binding shifts.** Removing an overload re-binds an untouched call site to a different
method. Ticket 04's whole-type widening covers the declaring type; the call site in another
assembly is the residue.

**The line through all three:** *additions are self-covering, removals and re-valuings are
not.* Adding `Foo(int)` alongside `Foo(long)` re-binds `Foo(1)`, but the added method is in
the changed set and the call site's IL now points at it, so the reverse walk finds it for
free. Delete `Foo(int)` and the affected call site's IL points at `Foo(long)`, which nothing
put in the changed set.

**Questions:**

- Is the unit of comparison the member declaration, and does that close the
  `const`/initializer/attribute gap on the declaring side?
- What is the consumer-side rule? The safe answer is that a changed signature or
  compile-time constant widens to every assembly that transitively references the declaring
  one, via the project graph. How often would that fire on a real pull request?
- Can the rule be narrowed to *public* surface without under-selecting? `InternalsVisibleTo`
  and `internal` consts consumed by a friend assembly are the obvious traps.
- This is the one place the **MVID comparison** the map is holding as a later cross-check
  would genuinely earn its keep — it would identify every assembly whose IL changed
  regardless of cause. It needs baseline binaries, which ADR-0001 rules out persisting.
  Is there a cheaper approximation, and is the whole problem better solved there?

Reaches into [Method identity and the performance budget](09-method-identity-and-performance-budget.md)
and [The unmappable-change rule table](15-the-unmappable-change-rule-table.md). Blocks
[Write the spec](12-write-the-spec.md): it is a correctness rule, and the spec cannot state
which direction change detection errs in without it.

## Comments

**From [The report contract](07-the-report-contract.md):** an **attribute-only change must
count as a changed member**, and the reason is sharper than the general
`const`/initializer/attribute-argument gap already listed above.

PRD §4.2 selects a test that is **new since the baseline**. Reach reads only the *current*
compiled output and cannot enumerate the baseline's tests without building it, so the only
affordable implementation is to derive newness from the change set: an added test method is
a changed member that happens to be a test.

That derivation breaks on a case with no attribute *argument* involved at all. **Adding
`[Fact]`, `[Test]` or `[TestMethod]` to an existing method creates a new test while its body
stays byte-identical.** A body-level hash sees nothing, the method never enters the changed
set, and the new test never runs — an under-selection, which the correctness rule forbids.
The same applies to `[Theory]`/`[TestCase]`/`[DataRow]` added to a method that was already a
test, and to a test class gaining an attribute that makes its methods discoverable.

So the answer to "is the unit of comparison the member declaration?" has a correctness
consequence beyond the consumer-side inlining problem: if the unit stays the body, ticket 07
needs a separate mechanism for `new-since-baseline`, and there isn't an affordable one.
Ticket 07 has kept `new-since-baseline` as a rule distinct from `own-source-changed` in the
report so that the two can diverge if this ticket decides they must.

## Answer

Two rules. The first is free and closes the declaring-side gap entirely; the second is the
only place in this map where the simple answer was *knowingly wrong* rather than merely
incomplete, so it is implemented rather than named —
[ADR-0014](../../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md).

### The unit of comparison is the declaration, not the body

A member's hash covers its **whole declaration with trivia stripped** — modifiers,
attributes, signature, parameter defaults, initializer and body — not just the body. Roslyn
is already parsing the file, so this costs nothing and closes the declaring-side list in one
move: `const` values, field initializers, enum members, attribute arguments and default
parameter values all sit inside the declaration.

It also resolves the escalation from [the report contract](07-the-report-contract.md):
**adding `[Fact]` to an existing method now registers as a change**, because the attribute is
part of the declaration. PRD §4.2 rule 3 (`new-since-baseline`) therefore stays derivable
from the change set, and ticket 07's decision to keep it as a distinct rule from
`own-source-changed` was right for reporting but does not need a separate detection mechanism.
There isn't an affordable one, and now none is needed.

**Type headers are their own unit.** A type's modifiers, attributes, base list and type
parameters hash separately from its members, and a change to that header is **whole-type
widening**. This covers the case the report contract raised last — a test class gaining an
attribute that makes its methods discoverable — which no member-level comparison would see,
because no member changed.

Stripping trivia is what preserves the charting decision that comment and formatting churn
must not select. That property now has to hold over a larger syntactic surface, which makes
it worth a fixture rather than an assumption.

### Recompilation widening, on a deliberately narrow trigger

The consumer-side problem is real and unfixable by anything on the declaring side: a changed
`const` is inlined into consumers whose source is byte-identical, and after inlining the
consumer's IL may not reference the declaring assembly at all — so whole-assembly widening
on the declaring assembly does not reach those tests.

**Trigger** — only these, and both are the ticket's own "removals and re-valuings are not
self-covering" line:

- a **removed** member of any kind;
- a changed **compile-time constant**: a `const` value, an enum member value, or a default
  parameter value.

**Effect**: whole-assembly widening for every in-scope assembly instance that **transitively
references** the declaring assembly, through the project graph — which is already built and
is far coarser than the call graph, so this is a graph walk over dozens of nodes, not
millions.

**Additions trigger nothing.** Adding `Foo(int)` alongside `Foo(long)` rebinds `Foo(1)`, but
the added member is in the changed set and the call site's IL now points at it, so the reverse
walk finds it without help. A changed signature is a removal plus an addition, so the removal
half already fires.

**Not narrowed to public surface.** `InternalsVisibleTo` and internal consts consumed by a
friend assembly are exactly the traps the ticket named, and detecting the friend relationship
correctly costs more than the widening saves.

**Attribute arguments do not trigger it.** An attribute argument is baked into the declaring
assembly's own metadata, not inlined into consumers — so the declaration-level hash above is
sufficient and the trigger stays narrow. Worth stating because the three gaps arrive together
in the ticket and only two of them cross the assembly boundary.

### What it costs, and why that is visible rather than hidden

A `const` change in a widely-referenced core assembly widens most of the solution. That is
the accepted cost, and it is the reason the trigger is two cases rather than "any signature
change". Crucially it is **legible**: ticket 07's forward change list carries every change with
its tier and the count of tests it reached, so a PR that selected everything shows *which*
change did it. A user can see the `const` and move it, which is a better outcome than a tool
that silently under-selects on the same input.

### MVID stays out

MVID comparison would identify every assembly whose IL changed regardless of cause, and it is
the honest fix for this whole class. It needs the baseline's binaries, which means building the
baseline —
[ADR-0001](../../../docs/adr/0001-reach-persists-no-state-between-runs.md) rules out persisting
them and nothing in M1 builds a second revision. Recorded on
[the limitations register](19-the-limitations-register.md) as the named upgrade path, not
attempted.

### Consequences for other tickets

- **[The unmappable-change rule table](15-the-unmappable-change-rule-table.md)**: a package
  version change alters constants Reach cannot see in source at all, so it stays that ticket's
  business rather than being covered here.
- **[Fixture catalogue](11-fixture-catalogue.md)**: three fixtures — a changed `const` consumed
  across an assembly boundary, a removed overload rebinding an untouched call site, and
  `[Fact]` added to an existing method. The last is the cheapest test of the most expensive
  possible regression.
- **[Write the spec](12-write-the-spec.md)**: both rules are correctness rules and each states
  its direction.
- **PRD §11** lists "rule 3 depends on attribute-level change detection" as open and owed a
  decision before change detection is fixed. It is now decided, which makes this a candidate
  line for the next PRD amendment rather than a live risk.
