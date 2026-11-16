# Migrating the Contoso acceptance tests

Type: grilling
Status: open
Blocked by: 10 (04 resolved)

## Question

Contoso becomes the first small sample solution, and its 13 acceptance tests
(`tests/Reach.Tests/Acceptance/AcceptanceTests.cs`) move onto patches. Not all of them are
selection checks: report determinism, the consumer loop over `invocations`, and the committed
example report (`ExampleReportTests`) assert other things.

For each of the 13 plus `ExampleReportTests`: does it become an acceptance test, stay as C#
over the new harness, or move elsewhere? And does the Contoso fixture itself need to change
to fit the declaration format?

## Comments

- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):** there are **11** Contoso acceptance tests, not 13. The four that assert more
  than selection (NUnit `~`, determinism, zero-count change, runner-setting notice) now fit the
  layer: the expected result gains will-run, notices and changes, and **every** acceptance test
  checks determinism. Whether Contoso becomes the "frameworks" sample is decided in
  [The small sample solutions](10-the-small-sample-solutions.md).
- **From [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md):**
  Contoso moves from `tests/fixtures/Contoso/` to `tests/samples/Contoso/`, and its tests become
  one class in `tests/Reach.Tests/Acceptance/Contoso/` with `.patch` files beside it; today's
  `Touch` calls become patches. `tests/fixtures/` goes once it is empty.
- **From [How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md):**
  `FixtureSolution` is **reworked**, not deleted, into the per-test sample copy. This migration
  removes its old API (`WriteFile`/`Touch`/`Rebuild`) once no caller is left. Determinism is
  checked inside `Expect` with `timings` scrubbed, so today's standalone determinism test has
  nothing left to assert.
