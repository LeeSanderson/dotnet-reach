# How the harness builds each acceptance test

Type: grilling
Status: open
Blocked by: (none — 04 resolved)

## Question

[What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md)
found three facts. Reach needs no baseline build. A copied built tree recompiles everything.
Parallel runs are correct with one directory per test. Given those, what does the harness do
for each acceptance test?

- **The build shape.** Copy the sample solution's sources plus `git init` and one build of the
  patched tree (about 4–6 s per test)? Or a pool of same-path built trees, restored between
  tests (incremental, but one test per directory, and `bin/`/`obj/` must be restored with the
  sources)?
- **How the baseline commit is made**, now that it is not built: the unpatched sample committed,
  the patch applied to the working tree.
- **Parallelism.** xUnit v3 runs one class's tests one at a time. If acceptance tests are one
  theory, or one class, how are they spread to run together, and with how many at once?
- **What replaces `FixtureSolution`**, and what happens to its stale class-fixture comment.

Depends on whether an acceptance test is data or code, from
[How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md).

## Comments

- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):** every acceptance test runs Reach **twice** and compares the two
  `report.json` files byte for byte. Measure what the second run costs next to the build; if it
  is not small, bring it back to the owner. Acceptance tests run Reach **in-process**
  (`ReachPipeline.SelectAsync`), as `FixtureSolution` does today.
- **From [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md):** the
  harness surface is `Apply("a.patch", ...)` then `.Expect(new Expected { ... })`, one class per
  sample solution under `tests/Reach.Tests/Acceptance/<Name>/`, samples copied from
  `tests/samples/<Name>/`. Patches are read from the source tree. **Determinism is checked
  inside `Expect`** (two runs of Reach, byte-identical `report.json`), so its cost is this
  ticket's. `Expect` reports every differing section at once, `Because` first.
- **From [Where the acceptance tests run](07-where-acceptance-tests-run.md):** there is **no time
  budget**, so parallelism is about the developer's wait, not a limit to stay under. Acceptance
  tests run by default in a plain `dotnet test`, so their wall-clock is every developer's.
