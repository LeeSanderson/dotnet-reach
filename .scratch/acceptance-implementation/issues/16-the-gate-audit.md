# The gate audit

Status: ready-for-agent
Depends on: 01–15
Spec: [§9](../../acceptance-layer/spec.md#9-the-release-gate)

## Goal

Confirm that every claim in the alpha's docs is proven by a named, passing test. Then the release
is unblocked.

## Scope

1. **Fill in the table.** In spec §9.2, replace every "ticket NN" with the real test name (file and
   method, relative to `tests/Reach.Tests/`). Remove the rows for claims that were cut. The table
   stays hand-kept.
2. **Walk the docs again.** Read `README.md`, `docs/adopting-reach.md`, `docs/report-schema.md`,
   `select --help`, and the "Detectable" lines of `docs/limitations.md` that name a notice code. Any
   claim not in the table is either added with its test, or reported. **Do not cut or add claims
   here.** A new unproven claim is a finding for the owner.
3. **Resolve the recorded findings.** Collect what tickets 07 (row 29), 10 (row 23) and 14 recorded
   as Reach contradicting its docs. For each, bring the owner the choice (change the docs or change
   Reach) and record the answer. Nothing in the gate may still contradict Reach when this ticket
   closes.
4. **Run the gate.** The whole suite green on both CI legs, and the release-gate run (every test
   except `Batch=known-gap`, using ticket 09's filter) green as well. Link the CI run in this
   ticket's comments.
5. **Unblock the release.** Mark this ticket resolved. Publishing (`m1-implementation` ticket 21)
   then has its dependencies met.

## Acceptance criteria

- Spec §9.2 names a real test for every gate claim, and every named test exists.
- Every finding from steps 2 and 3 has an owner decision recorded.
- Both CI legs are green on `main`, with the link recorded.
- The recipe's shell-loop claim is noted as proven only once dogfood runs against the published
  package. That is expected, and it is not a blocker for publishing (spec §9.2).

## Out of scope

Publishing itself (`m1-implementation` ticket 21, `ready-for-human`).
