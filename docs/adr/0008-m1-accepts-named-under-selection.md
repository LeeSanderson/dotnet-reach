# M1 accepts named under-selection rather than widening for every hole

M1 ships with known under-selection holes, each named in a **limitations register** and
surfaced in the report, instead of widening the selection until every hole is closed. The
walking skeleton is a proof of concept, and working code with stated limits is worth more
than an exhaustively-specified tool that does not exist; coverage grows by closing named
holes, one at a time, against real measurements.

A future reader will find this surprising, because PRD §8 and the walking-skeleton map both
state that the correctness rule outranks everything and that genuine uncertainty resolves
by widening. This is a conscious override of that rule, taken by the owner, and it is
recorded here so that it does not read as an oversight or get "fixed" by someone who
assumes it was one.

## Why this does not break the product claim

PRD §10 already scopes the claim to conservatism *plus disclosure*: "its claim is that it
is conservative and that it says so when it cannot see." A named, reported hole keeps that
promise. A silent one breaks it.

So the register is load-bearing rather than documentation hygiene. The decision holds only
while every accepted hole is written down and visible to a user deciding whether to trust a
selection — which is why the register is a deliverable of M1 and not a follow-up.

## The holes accepted at M1

Enumerated by [Generics, delegates and function
pointers](../../.scratch/walking-skeleton/issues/05-generics-delegates-and-function-pointers.md);
owned and kept current by [The limitations
register](../../.scratch/walking-skeleton/issues/19-the-limitations-register.md).

The largest is **first-party members invoked only from outside the analysis scope**.
Verified: `$"{n}-{o}"` emits `DefaultInterpolatedStringHandler::AppendFormatted<T>` and
never names `ToString`, so a test exercising an override only through interpolation cannot
reach it. It generalises past the BCL to every framework base-class override —
`BackgroundService.ExecuteAsync`, `DbContext.OnModelCreating` — that a host calls and
first-party code never does.

The rest: function pointers obtained outside analysed IL (`Delegate.CreateDelegate`,
native interop), and deserialization or reflective construction, which defeats any
argument leaning on a constructor being visible.

## The rejected alternative, kept as the upgrade path

**Whole-type widening on the declaring type** whenever a changed member overrides or
implements a contract declared outside the analysis scope. It leans on the constructor as a
choke point: an object the host calls back into had to be constructed by first-party code,
and `.ctor` is a member of the type, so widening the type reaches any test that constructs
it even when the test only ever holds it as `object`.

Not adopted under this decision, but written down rather than discarded, because it is the
cheapest known upgrade — it needs only the base-type walk and `MethodImpl` table that
widening already builds — and because M2's framework models supersede it. `MethodSpec`
carries the type argument at the call site even though
[ADR-0006](0006-a-graph-node-is-an-il-method-definition.md) drops it from identity, so a
model can say "`AppendFormatted<T>` calls `T.ToString()`" and resolve `T`. The hole has a
route out; it is not a ceiling.
