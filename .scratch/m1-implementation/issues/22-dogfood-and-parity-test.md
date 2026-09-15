# dogfood.yml and the docs↔workflow parity test

Status: resolved
Depends on: 21
Spec: [§16.4](../../walking-skeleton/spec.md#164-documentation-ci-and-publishing)

## Goal

Reach runs on Reach, and the CI recipe in the documentation is **provably** the YAML this
repository runs. The last ticket in M1.

> **This cannot be written earlier, and that is the point.** The documented recipe is a
> `pull_request` workflow installing a **published** package. Pre-publish, there is no package to
> install, so a parity test would be comparing two things that differ on the line that matters
> most. Sequencing dissolves the bootstrap: **you cannot dogfood before you publish, and you do not
> need to.**

## Scope

**`dogfood.yml`**, from the first tag onward:

- `actions/checkout` with **`fetch-depth: 0`** — the first line, and the one that makes the recipe
  work at all;
- `dnx dotnet-reach@<version>` with the version **pinned**, since a bare id will not resolve a
  prerelease;
- `dotnet reach select`;
- the selection run **alongside a full suite that keeps gating**.

**Reach runs on Reach and does not gate.** Gating M1's own CI on M1's own selection would be the
unproven narrowing PRD §8 forbids, and the instrument that would justify it is shadow mode, which
is M2. **Running both *is* a miniature shadow mode**: the full suite tells the truth, the selection
says what Reach would have run, and the gap between them is **the first real datapoint** — which is
also what the performance budget deferred to when it declined to commit a wall-clock number. It
stops being a second job the day M2 proves it can.

The reasoning lives in this ticket and in the CI decision record rather than in an ADR: the
question a future reader is most likely to ask — *why does the test-selection tool not gate its own
CI on its own selection?* — is surprising and is a real trade-off, but it is **one line to
reverse**, so it fails the hard-to-reverse test.

**It is a separate workflow file, not a job inside `ci.yml`**, because the parity test compares a
fenced block against **a workflow file**; comparing it against one job of a larger file is a worse
test of a worse artifact.

**The parity test** is a test in the suite, not a CI step. It compares the fenced YAML block in
`docs/adopting-reach.md` against `dogfood.yml` and asserts they are **byte-identical with nothing
elided**.

Why it exists, and it is the one carve-out in an otherwise judged adoption criterion: ADR-0010
means **Reach does not work under default CI settings**, so exit 4 is the likeliest first run
anyone has, and a wrong fetch-depth line in the docs breaks PRD §12's under-an-hour criterion **on
contact**. Everything else about adoption cost is judged; this one line is asserted.

**Record the first datapoint.** The first dogfood run produces numbers nothing else in M1 has:
Reach's phase timings on a real solution, and the over-selection ratio with its `widened` /
`compiled` split. Write them into this ticket's Answer section and check them against the stop
table — not because M1 is gated on them, but because *"the first real run is the first datapoint"*
is what the performance budget and the measurement both deferred to, and a number nobody wrote down
is a number nobody has.

## Acceptance criteria

- `dogfood.yml` exists, runs on `pull_request`, and its first checkout step sets `fetch-depth: 0`.
- The full suite still gates; the Reach step does **not** fail the build on a non-empty or empty
  selection.
- **The parity test fails when one character of the fenced block in `docs/adopting-reach.md`
  differs from `dogfood.yml`** — asserted by perturbing one and observing the failure.
- The `dnx` line in the fenced block carries a version pin.
- A dogfood run on a real pull request completes and writes `.reach/report.json` as a workflow
  artifact, so the numbers are inspectable.
- **The first datapoint is recorded in this ticket's Answer**: phase timings, the over-selection
  ratio, and the `widened`/`compiled` split, read against the stop table.

## Out of scope

Gating on Reach's selection. Shadow mode — M2, and it is what would justify gating. Acting on the
first datapoint beyond recording it: if it lands in the stop table's bottom two rows, that is a
finding for the owner, not a change to make here.

## Answer: the first datapoint

**Measured locally, not from a CI run**, because `dogfood.yml` installs a package that is not
published yet — ticket 21's human steps are the gate. The numbers come from the same binary the
workflow would install, run against this repository with `select -c Release --no-build`, so only
the provenance differs. Re-take them from the first real run and replace this section.

### A one-member change — `SpanJoin.Resolve`

| | |
|---|---|
| ratio | **81 / 430 = 19%** |
| widened pairs | **0** |
| path class | 81 `compiled`, 0 `widened` |
| changes | 1, at `member` tier, reaching 81 |
| Reach's own wall clock | 1.4 s total — `changes` 423 ms, `graph` 338 ms, `baseline` 153 ms, everything else under 50 ms |

**Against the stop table: the top row, comfortably.** One mid-graph method in `Reach.Core`, joined
at member tier, reached a fifth of the suite over compiled paths only. This is the shape the tool
was built for, and it is the first evidence that the join and the reverse walk do on a real
codebase what the fixtures say they do.

### A five-commit range — this session's work

| | |
|---|---|
| ratio | **430 / 430 = 100%** |
| widened pairs | **0** |
| changes | 88 — 50 `member`, 1 `whole-type`, 37 `whole-assembly`; 41 of them reached nothing |
| delivery | `response-file` — 430 tests exceeded the command-line ceiling, as designed |

**The cause is legible and correct**: the range contains a change to `src/Reach.Cli/Reach.Cli.csproj`,
which routes at whole-assembly tier, and `Reach.Tests` references `Reach.Cli`. Everything is
reachable, so everything is selected. Widening working exactly as specified — a five-commit range
spanning a packaging change is also not a pull request, which is why the stop condition says
*median*.

### The finding: `widenedPairs` cannot see tier widening

That 100% row reads, on `summary` alone, as *"over 70%, mostly `compiled`"* — the stop table's
bottom row, **"not Reach's failure, the codebase is too connected"**. That reading is wrong, and it
is the exact misreading the table calls the expensive one.

Path class describes the edges walked. A whole-assembly widening walks none: every method is
already a root, so its tests are reached at distance zero and class `compiled`. **A run that
widened an entire assembly reports `widenedPairs: 0`.**

Registered as an extension of *"the widening delta understates widening's contribution"*, with the
reading that recovers the truth — `changes[].tier` — added to the stop condition's guidance. Not
acted on further: this ticket records the datapoint, and the instrument's upgrade is M2's.

### The bug the first run found

`testsReached: 0` on a change whose tier said `whole-assembly` is arithmetically impossible, and it
was: **widening a test assembly selected none of the tests in it**. Two `const` fields in a test
class cannot join, fell through to whole-assembly widening, and selected nothing. Under-selection
out of an over-selection mechanism — fixed in its own commit with a test at the `Selector` seam,
before these numbers were taken.

This is the whole case for dogfooding. Five hundred tests, a fixture solution and an acceptance
suite did not find it; one real run did, and it found it in the report rather than in a stack
trace — which is also the case for `testsReached` existing at all.

## Implementation notes

**The recipe in the documentation had a bug the parity test would not have caught**, and writing
the workflow for real is what surfaced it. The original fenced block piped `jq` into
`while read … do eval "$command"; done`, so a failing selected test left the step **green** —
`eval`'s status inside a loop goes nowhere. Both sides now track a status and exit with it, and
skip the blank line an empty selection produces. Verified in three directions: a failing command
exits 1 even when a later one succeeds, an empty `invocations` array runs nothing and exits 0, and
the shell parses under `bash -n`.

**The parity test asserts more than equality**, because equality alone is satisfied by two
identically wrong files: the block must carry `fetch-depth: 0` and a pinned `dnx dotnet-reach@`,
and must not carry the bare form. The perturbation test changes `fetch-depth: 0` to `1` — the one
character this test exists for — and was also checked by hand against the real files, where it
fails two assertions and passes again on revert.

**Line endings are normalised and nothing else.** Git hands a Windows checkout CRLF and a Linux one
LF, and a parity test that failed on the platform rather than on the content would be reporting the
wrong thing on the one line it protects.

**`dogfood.yml` will fail on every pull request until the package is published.** That is the
sequencing this ticket names, not a defect.
