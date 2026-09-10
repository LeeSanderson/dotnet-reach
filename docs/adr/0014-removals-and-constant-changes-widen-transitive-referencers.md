# Removals and constant changes widen transitive referencers

A **removed member**, or a changed **compile-time constant** (a `const` value, an enum member
value, or a default parameter value), puts every in-scope assembly that *transitively
references* the declaring assembly into whole-assembly widening — via the project graph, not
the call graph.

A reader will find this alarming: one changed `const` in a core assembly can widen most of a
solution, and nothing else in Reach's design reaches that far from a single change. It is
deliberate, and the alternative is silent under-selection.

## Why the declaring side cannot cover it

Reach computes the changed set by comparing source between two revisions. The compiler bakes
compile-time constants into every consumer, so after inlining:

- the consumer's **source is byte-identical**, and nothing puts it in the changed set;
- the consumer's **IL is different**, because the literal changed;
- worse, the consumer's IL may no longer **reference the declaring assembly at all**, since an
  inlined constant leaves no trace of where it came from.

So whole-assembly widening on the declaring assembly does not reach a test that exercises the
consumer. The same shape applies to a removed overload: an untouched call site in another
assembly rebinds to a different method, and its source never changed.

## The trigger is narrow on purpose

Additions are self-covering, which is what makes a narrow trigger sufficient. Adding
`Foo(int)` alongside `Foo(long)` rebinds `Foo(1)`, but the added member *is* in the changed set
and the call site's recompiled IL now points at it, so reverse reachability finds it for free.
A changed signature decomposes into a removal plus an addition, so the removal half fires.

That leaves removals and re-valuings, which are a minority of changes in a typical pull
request. The rule is broad in effect and rare in firing, which is the only reason a
project-graph-wide widening is affordable at all.

Two narrowings were considered and rejected. **Restricting to public surface** founders on
`InternalsVisibleTo` and internal consts consumed by friend assemblies — detecting the friend
relationship correctly costs more than the widening saves. **Including attribute-argument
changes** in the trigger is unnecessary: an attribute argument lands in the declaring
assembly's own metadata and is not inlined into consumers, so declaration-level change
detection already covers it.

## Considered options

**Name it as an accepted hole** under
[ADR-0008](0008-m1-accepts-named-under-selection.md) and
[ADR-0012](0012-m1-resolves-open-design-questions-at-80-20.md), like the other residues. This
was live and was rejected: the rule is roughly ten lines over a project graph that already
exists, so the cost of correctness here is close to zero, and a wrong answer about `const`
changes is the kind of thing that destroys trust in a selector the first time it happens.

**MVID comparison** is the honest general fix — it identifies every assembly whose IL changed,
whatever the cause, including cases this rule misses. It requires the baseline's binaries,
which means building a second revision; [ADR-0001](0001-reach-persists-no-state-between-runs.md)
rules out persisting them and nothing in M1 builds the baseline. Recorded on the limitations
register as the upgrade path.

## Consequences

The blast radius is **legible rather than hidden**, which is what makes it acceptable. The
report's forward change list carries every change with its tier and the count of tests it
reached, so a pull request that selected the whole suite shows exactly which `const` did it.
A user can then move the constant, or accept the cost knowing why — both better outcomes than
a quietly missing test.

It also means the project graph is load-bearing for correctness, not just for scope
resolution. Previously it only decided what to analyse; now a bug in it is an under-selection
bug.

**Revisit trigger:** measurement showing this rule fires on a large share of real pull
requests. That would mean the codebase keeps its constants somewhere hot, and the response is
a narrower trigger or the MVID upgrade, not deletion.
