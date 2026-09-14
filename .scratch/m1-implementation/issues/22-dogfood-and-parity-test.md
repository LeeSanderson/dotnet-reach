# dogfood.yml and the docs↔workflow parity test

Status: ready-for-agent
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
