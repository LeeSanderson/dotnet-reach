# What counts as a changed member, and cross-assembly recompilation effects

Type: grilling
Status: open
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
