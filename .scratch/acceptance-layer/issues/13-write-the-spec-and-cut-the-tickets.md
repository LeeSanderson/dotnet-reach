# Write the spec and cut the implementation tickets

Type: task
Status: resolved
Blocked by: (none — 12 resolved)

## Question

Write `.scratch/acceptance-layer/spec.md` from the map's Decisions so far, then cut the
ready-for-agent implementation tickets into a separate effort, in the same shape as
`walking-skeleton/` → `m1-implementation/`. The spec carries the claim-to-suite table from
[Which suite proves each claim](11-which-suite-proves-each-claim.md), amended by
[Test it or cut it](12-test-it-or-cut-it.md).

Done when the spec is written, the tickets exist, and the map's destination is reached.
That lifts the `On hold:` line on [Publishing](../../m1-implementation/issues/21-publishing.md).

## Answer

Done 2026-11-22. **[spec.md](../spec.md) exists, and the sixteen implementation tickets live in a
separate effort at [acceptance-implementation/](../../acceptance-implementation/README.md).** They
are kept out of this map's `issues/` so a wayfinder session never mistakes a build ticket for a
decision.

**Staged in four parts.** Stage A (01–03) is the harness: parallelism and CI steps, then
`Baseline`/`Apply`/`Expect` proven by row 1 on a first cut of Basics, then the patch tool. Stage B
(04–05) is what the gate needs from Reach and its docs: emit the two missing notices, then cut and
cite. Stage C (06–12) is the samples, ending with Frameworks, which deletes what is left of
`AcceptanceTests.cs` and `tests/fixtures/`. Ticket 12 depends on every ticket whose rows replace an
old test, so the no-gap rule is enforced by the dependency graph. Stage D (13–16) is the CLI, unit
and parity tests, and a closing **gate audit** that fills the claim table with real test names. All
sixteen are `ready-for-agent`.

**The claim-to-suite table is in the spec (§9.2)**, amended by ticket 12. Each claim names an
existing test, a row, or the implementation ticket that adds the proof. The gate audit replaces
the ticket references with real test names.

**Things writing the spec had to settle that no ticket had:**

- **`--list-unselected` is removed** (owner decision, this session). It turned out to be parsed and
  never read: `SelectRequest.ListUnselected` has no reader. M1 spec §13.5 is amended to "counted,
  never enumerated". This is the same move as cutting `calli-unresolved`.
- **Two new rows.** Row 28 is ticket 12's build-breaking patch, which proves exit 2. Row 29 covers
  `all-changes-outside-analysis-scope`: ticket 12 said the `nothing-selected` rows prove all three
  reason codes, but rows 13 and 15 cover only two. Basics gains `Basics.Tools`, a project no test
  reaches.
- **How row 28 gets exit 2** (spec §5.2). The harness builds the patched tree itself, as ticket 09
  decided. If that build fails, the harness runs Reach in default build mode, so Reach reports the
  failure itself. This needs no new API, and a row that breaks the build by accident fails on its
  required `Outcome`.
- **`ExitCode` joins `Expected`**, required exactly when `Outcome` is `Failed`. Every other outcome
  exits 0, which is how `Expect` asserts the exit code for every outcome.
- **Two claims missing from ticket 11's unproven list**: `tests[].id` "comparable across runs" and
  "reserved `data` keys mean the same thing everywhere". Both are routed to cut-or-narrow in
  ticket 05.
- **`framework`/`dialect` are proven by the example report**, not by a new `Expected` section. The
  example holds a recognised project and the unrecognised one, and is byte-pinned.

**Publishing's hold is lifted.** Its `On hold:` line is replaced by a dependency on the gate audit:
planning is done, and the release now waits for the gate itself.
