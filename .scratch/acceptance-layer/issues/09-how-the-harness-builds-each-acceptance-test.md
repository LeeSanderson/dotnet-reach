# How the harness builds each acceptance test

Type: grilling
Status: resolved
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
- **From [Writing a patch and keeping it applying](08-writing-a-patch-and-keeping-it-applying.md):**
  the harness applies with `git apply --verbose` (never `--3way`). A failed apply reports the
  patch name and its position in the list (`2 of 3: x.patch`), git's full output, and the fix
  line `dotnet run tests/tools/patch.cs -- apply <Sample> <name>, resolve, then save --force`.
  Patches are **independent**: each applies to the clean sample alone, so order never matters
  for applying.

## Answer

Settled with the owner over three rounds of grilling (2026-11-16).

### Build shape: a fresh temp root per test

Each acceptance test gets its own temp root, deleted when the test ends: copy the sample, one
`dotnet build` of the patched tree (4–6 s), run Reach. Nothing is shared, so no test's result
depends on which test ran before it.

- **Disk was the worry, and it does not separate the options.** Each root is deleted on
  dispose, so peak disk is *(tests running at once) × one built sample*. A pool of slots needs
  exactly the same.
- **A pool inside one run was rejected.** Each slot pays one full build on first use, and only
  reuses are incremental (~1.5 s). With one slot per concurrent test (Ledger's ~9 tests on a
  20-core machine), no slot is ever reused. The pool trades parallelism for incremental builds,
  which only pays at dozens of tests per sample.
- **The named upgrade is persistent slots between runs** (e.g. `%TEMP%/reach-acceptance/<Sample>/<n>`,
  reset with `git reset --hard` + `git clean -fd`, keeping the ignored `bin/`/`obj/`), which
  makes a developer's second run incremental throughout. It costs staleness detection (sample
  sources, Reach build) and hermeticity. **Trigger: the CI acceptance step's wall-clock goes over
  5 minutes on either OS.** `Apply(...)` hides the build shape, so the switch changes no test.

### Baseline

The sample is copied into `repo/`, `.gitignore` (`bin/`, `obj/`) is written, and everything is
committed as `baseline`. The patches are `git apply`'d to the working tree and **left
uncommitted**; Reach runs with `--base <baseline sha>`. Reach's change set covers committed,
staged and unstaged changes alike, so the result is the same as a PR commit, and `HEAD` stays the
baseline. Accepted difference from a real PR: a patch that adds a file leaves it untracked, which
Reach always includes.

### Parallelism: `ParallelMode.All`

**Corrects [ticket 02](02-building-a-patched-sample-cheaply.md)'s "xUnit v3 runs one class's
tests one at a time".** `xunit.v3` 4.0.0 (this repo's version) adds `ParallelMode.All`, full test
parallelism within the assembly, including methods of one class, with `DisableParallelization`
on a class or method to opt out. The mode is per assembly.

- `Reach.Tests` sets `ParallelMode.All`; `maxParallelThreads` stays at its default (core count).
- **The implementation ticket's first step**: run the full suite (~440 in-memory tests included)
  under `All` and mark whatever breaks with `DisableParallelization`. If more than a handful
  break, fall back to `Collections` and record the per-sample floor (Ledger ≈ 1 minute serial).
- Rejected: `Collections` mode as the plan (a sample's tests serialise), and harness-started
  concurrent builds in a class fixture (patches are named only inside method bodies, so it fights
  [code, not data](04-how-an-acceptance-test-is-declared.md)).

### `FixtureSolution` is reworked, not deleted

It becomes the per-test sample copy (named e.g. `SampleCopy`): it already does copy → temp git
repo → commit → build → in-process `SelectAsync`. It gains a sample-name parameter and patch
application. Its stale class-fixture comment goes. Its old API (`WriteFile`/`Touch`/`Rebuild`)
goes when [Migrating the Contoso tests](06-migrating-the-contoso-tests.md) moves its callers.

### Determinism: two runs, a small scrubber, a string comparison

Measured on Contoso (20-core Windows, Release): a Reach run is **~1.05 s wall-clock, ~790 ms
in-process** (`totalMs`), against a warm build of 3.5–5.5 s. The second run adds ~20–25% and
stays; nothing to bring back to the owner.

- **Two runs are never byte-identical as written**: `timings` (`startedUtc`, `totalMs`, phases)
  differs every run, as `docs/report-schema.md` already says. The check is the schema's promise,
  not raw bytes.
- An **in-house scrubber** removes `timings` and replaces the temp root with a token; the two
  results are compared as plain strings, and a mismatch prints a diff.
- **No Verify (VerifyTests) dependency.** Its snapshot mode (`.verified` files, approval) would
  reverse [ticket 08](08-writing-a-patch-and-keeping-it-applying.md)'s "never generated, no
  snapshot mode", and for one in-memory comparison it is a dependency plus a file workflow the
  layer has ruled out.

### Keeping the copy clean

Reach treats untracked files under the root as changes (the measuring run's own log files showed
up as `unmapped-file-no-project` notices). So each test's temp root holds **`repo/`** (the sample
and the patch result, nothing else) and **`out/`** (reports, logs) side by side. Before the first
Reach run, the harness asserts `git status --porcelain` lists only the paths the patches touched.

### Not changed

No `CONTEXT.md` change and no ADR: test-infrastructure decisions, each reversible behind `Apply`.
