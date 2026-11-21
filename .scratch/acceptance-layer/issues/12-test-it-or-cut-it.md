# Test it or cut it: the claims no suite proves

Type: grilling
Status: resolved
Blocked by: (none — 11 resolved)

## Question

[Which suite proves each claim](11-which-suite-proves-each-claim.md) lists 28 claims in
`README.md`, `docs/adopting-reach.md` and `docs/report-schema.md` that no test proves, in four
groups. The gate rule says each one must be **tested or cut** before `0.1.0-alpha.1`. For each
claim, which one?

- **A. Documented, but Reach never does it** (three notice codes are never emitted; the
  README's bare `--filter` line). The choice is to implement it or to cut it from the docs.
  Acceptance row 18 expects `recompilation-widening`, so that row depends on this answer.
- **B. Reach does it, but no test checks it.** Which suite gets the test: an acceptance row's
  expected result, or a CLI or unit test?
- **C. Prose that drifts from what is pinned.** Add a parity test (like `RegisterParityTests`
  and `WorkflowParityTests`) or cut the prose?
- **D. Claims about other people's tools.** These are outside the gate rule as written. Keep
  them as cited facts, or cut them?

Three side questions from that ticket:

- Does the expected result gain a **`reasons`** section, so `nothing-selected` rows can prove
  the three reason codes?
- Are **`limitations.md`** and the **`--help` text** docs whose claims are in the gate? (The
  help already disagrees with the schema about `--paths`, and lists `--list-unselected`, which
  the schema does not mention.)
- Does the claim-to-suite table in the spec stay hand-kept, or is part of it checked?

## Answer

Resolved 2026-11-21 with the maintainer. Item numbers are [ticket 11](11-which-suite-proves-each-claim.md)'s.
A re-audit of PR #1's doc changes (merge `d66c029`) found that none of the 28 items had changed
status. It also found new claims, which are covered below.

### A. Documented, but Reach never does it

1. **`calli-unresolved`: cut.**
   - Remove it from the schema's notice index.
   - Set [the function-pointer register entry](../../../docs/limitations.md) to "Detectable: no".
   - Detecting it needs new IL dataflow work, which is a follow-up after the alpha and is out of
     scope here.
2. **`recompilation-widening`: implement.**
   - The widening exists already (`RecompilationWidening.From`), so the work is only to raise the
     notice.
   - Acceptance row 18 keeps expecting the notice.
3. **`langversion-above-ceiling`: implement.**
   - Raise the notice from the existing `ExceedsCeiling` check.
   - It is reported, not widened, as the schema says.
4. **README `--filter "$(…)"`: rewrite.**
   - Replace the line with the real recipe from `adopting-reach.md`, or with a link to it.
   - Reach gets no new output mode to make the README line true.

### B. Reach does it, but no test checks it

- **Expected result:** it gains an optional **`reasons`** section, checked exactly when given, like
  `notices`. The `nothing-selected` rows prove item 16.
- **Exit code:** `Expect` asserts the exit code that matches the outcome on **every** acceptance
  test, next to the invariants it already checks.
  - This proves item 7.
  - Basics gains one row whose patch breaks the build (outcome `failed`). This proves item 6.
- **Item 11, `packageVersion`: remove it.**
  - Delete the schema row and the unused `ReportEntry.PackageVersion` property.
  - `dialect` already records what the version decided.
  - Reach knows only the referenced assembly's version, which can differ from the package
    version, so emitting it would mislabel it.
- **The other items:**

  | Items | Suite |
  |---|---|
  | 5, 8, 9: exit 70; a report on exits 2/4/5/70; `summary` absent on early failure | CLI tests, one per exit code. Exit 70 needs a way to make the pipeline throw in a test |
  | 10: `baseline` absent on exit 4; `head` absent when git is unreadable | CLI tests, in the same class |
  | 12–15: `runnerHost`, `delivery`, `willRun` 0 on an unknown total, `ratio` to four places | Report-writer unit tests |
  | 17: `selection-delivered-out-of-band` | An assertion added to the existing long-selection CLI test |
  | 18: TeamCity not detected | A unit test on CI detection |
  | 19: `--artifacts-path` narrows discovery | A unit test on discovery |
  | 20: `.reach/` files remain after the process exits | One out-of-process smoke test |
  | 21: `changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` | The example-report parity test (C22), if the Frameworks row-7 example contains all four. Any it lacks get a report-writer unit test |
  | New: `unmapped-deterministic-source-root`'s `data.roots`, and the notice firing for packages folders configured other ways | Assertions in `CorrespondenceTests` |

### C. Prose that drifts from what is pinned

- **22, the walk-through values:** a parity test against `example-report.json`.
- **23, the notice-code index:** a parity test against `NoticeCatalogue`. This test would have
  caught group A.
- **24, the recipe's shell loop:** the dogfood workflow proves it. `WorkflowParityTests` pins the
  doc to `dogfood.yml`, and dogfood runs the loop. That proof depends on `0.1.0-alpha.1` being
  published, because the Select step runs `dnx` against the published package. No extra test.

### D. Claims about other people's tools

- They are kept as **cited facts**, outside the gate.
- Each one carries a source link. Any claim without one is cut.

### Side questions

- **`limitations.md`:** it is in the gate, but **only** for its "Detectable" claims that name a
  notice code. Its gap descriptions and directions of failure are disclaimers, not promises.
  Known-gap tests pin some of them without being in the gate.
- **`select --help`:** it is in the gate.
  - A parity test checks that the help's option names equal the options the docs describe. The
    descriptions stay hand-kept.
  - Fix the `--paths` wording by hand.
  - Either document `--list-unselected` or remove it from the help.
- **The claim-to-suite table:** it stays **hand-kept** in the spec, and each gate claim names the
  test that proves it. The parity tests already cover every part that can be checked
  automatically.

### Where the work goes

None of this is done here. Every item becomes an implementation ticket cut by
[Write the spec and cut the implementation tickets](13-write-the-spec-and-cut-the-tickets.md), under
the acceptance-layer spec.
