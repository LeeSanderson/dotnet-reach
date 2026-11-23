# The gate audit

Status: ready-for-agent
Depends on: 01–15, 17–20
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

## Comments

**Owner decisions already taken (2026-11-23)**, from findings raised while implementing. Record
them, don't re-ask:

- **Row 12, a generator `.cs` edit selects nothing:** fix Reach, in [ticket 18](18-a-generator-source-change-widens-its-consumers.md).
- **Row 29, `all-changes-outside-analysis-scope` can't fire:** fix Reach, in [ticket 19](19-changes-outside-the-analysis-scope.md).
- **Row 5, a deleted member widens its whole assembly:** fix Reach, in [ticket 17](17-a-deleted-member-widens-only-its-type.md).
- **VSTest projects under a `global.json` that selects Microsoft.Testing.Platform** (found by
  ticket 12): the SDK refuses `dotnet test` on them, and Reach emits those commands with no notice.
  Decision: **a limitations entry**, not a notice. Add it to `docs/limitations.md` and mention it in
  `docs/adopting-reach.md`. Reach is unchanged for the alpha. Do this in this ticket.
- **The "unknown is never zero" invariant** was narrowed to its intent by ticket 08, with owner
  approval. Make spec §4.4 match.

**Findings collected for step 3**. Each needs the docs, the spec or Reach brought into line, and the
outcome recorded here. Where only the spec or docs need to change, change them and record that. Bring
anything else to the owner.

- Ticket 07, rows 13 and 14: a formatting-only change gives `no-changes`, while a README-only change
  gives `nothing-selected` with the reason `changes-were-formatting-only`. The docs and spec have
  these the other way round, and the reason's name misdescribes when it fires.
- Ticket 11, row 18: it also selects Dunning's `ReminderSenderTests`, through Api's whole-assembly
  widening and `IReminderChannel.Send`. That contradicts spec §7.2 and breaks standing rule 4.
  Reshape Ledger, or amend the spec.
- Ticket 10, row 22: a changed field is routed `whole-assembly`, because a field has no method to
  join. Check that the docs say so.
- Ticket 12: Frameworks has seven projects, not the eight spec §8.2 says.
- Ticket 03: spec §3.5's drift routine ("apply --3way, resolve, save --force") can't work, because
  a stripped patch has no `index` lines to merge from. The tool says to redo the change by hand.
  Reword §3.5.
- Ticket 02: the API is `ExpectAsync`, not `Expect`. Every report carries `baseline-resolved` and
  `baseline-is-head`. Make the spec match.
- Ticket 18: `docs/limitations.md` says generator detection also reads `IsRoslynComponent` and
  `EnforceExtendedAnalyzerRules`, but no code reads either. Only the `Analyzer` reference marker is
  used. Correct the doc. Also, a `Directory.Build.props` inside a generator's directory widens only
  the generator, so it selects nothing. That's the same under-selection shape as row 12. Bring it
  to the owner: fix it, or add a limitations entry.
- Ticket 19: a changed `.csproj` of an out-of-scope project still gets the reason
  `changes-were-formatting-only`. That's the same misnaming as the rows 13 and 14 finding.
- Ticket 05: two claims are still uncited: "Git does not preserve timestamps", and MSBuild's
  incremental-build claim.
- Ticket 09's release-gate filter, for step 4:
  `-- --filter-namespace "Reach.Tests.Acceptance*" --filter-not-trait "Batch=known-gap"`.

**Orchestrator's adjustments (2026-11-23)**, recorded as given:

- "Both CI legs green on `main`" becomes green on PR #2's branch, `acceptance-layer`. The PR merges
  to `main` only after review. CI runs only when the orchestrator pushes, so the run below is for
  the branch's head when this ticket started, not for this ticket's commits.
- The dogfood workflow's `dnx: command not found` is to be fixed here, in the workflow and the
  documented recipe together.
- Two owner calls are prepared, not made: row 18, and a `Directory.Build.props` inside a
  generator's directory. Every other finding is resolved by correcting the spec or the docs.
- `readme-only.patch` is re-saved.

**Done.** What changed:

1. **§9.2 filled in.** Every "ticket NN" is replaced by a real test (file and method). A new
   rows-to-tests table names each row's test. The rows for cut claims (`calli-unresolved`,
   `packageVersion`, reserved `data` keys) are gone. 183 test references in §9.2 were checked
   against the test sources by script; every one exists.
2. **Docs walked again.** Claims found in no row, now added with their tests: everything
   `select --help` says (a new `select --help` block: target discovery, `--base` auto-detection,
   `--report -`, the `.reach` default, `--no-build`, `NO_COLOR`); `-c`/`-v`/`--no-color` in
   `adopting-reach.md`; the outside-the-scope `changes` paragraph in the schema. New cited facts
   carry their source: `dnx` on `PATH`, Git and modification times, MSBuild incremental builds, and
   the VSTest/MTP refusal. No unproven claim was found, and no claim was cut or added beyond the
   decisions below.
3. **Findings resolved:**
   - **Rows 13 and 14 (ticket 07) and the out-of-scope `.csproj` (ticket 19): docs and spec
     corrected to Reach.** Spec §7.1 rows 13 and 14 now say what Reach does (formatting-only C# is
     `no-changes`; README-only is `nothing-selected` with `unmapped-file-no-project` and
     `changes-were-formatting-only`), with a note. `docs/report-schema.md`'s "different news"
     paragraph no longer calls a docs-only PR `no-changes`, and the index row for
     `changes-were-formatting-only` says what fires it (no member changed) and that its name
     misleads. The doc comments on `NoticeCodes.ChangesWereFormattingOnly` and
     `ReportBuilder.OutcomeOf` say the same. Comments only; Reach's behaviour and output are
     unchanged. Not changed, because it is Reach's output: the human summary's `no-changes` line
     ("Nothing in this working tree differs from the baseline") is not quite true for a
     formatting-only edit. Worth a later wording fix in Reach.
   - **Row 22, a changed field (ticket 10): registered.** No doc said it. `docs/limitations.md`
     has a new over-selection entry, "A changed declaration with no compiled body widens its whole
     assembly" (a field with no initialiser, a `const`, abstract, `extern`, bodiless `partial`).
     Spec §7.2 notes it under row 22.
   - **Frameworks has seven projects (ticket 12):** spec §8.2 corrected.
   - **§3.5's drift routine (ticket 03):** reworded. `apply` cannot merge a stripped patch; drift
     git can't absorb is redone by hand, then `save --force`. §3.4's `apply` line says the same.
   - **`ExpectAsync` and the two standing notices (ticket 02):** §4.2 shows `ExpectAsync` and says
     why; §4.3 says every report carries `baseline-resolved` and `baseline-is-head`.
   - **"Unknown is never zero" (ticket 08, owner-approved):** §4.4 now states the narrowed
     invariant.
   - **Generator detection markers (ticket 18):** `docs/limitations.md` corrected. Only the
     `Analyzer` project-reference marker is read; `IsRoslynComponent` and
     `EnforceExtendedAnalyzerRules` are not.
   - **Two uncited claims (ticket 05):** both now cited (the Git FAQ; MSBuild's incremental-builds
     page), not cut.
   - **VSTest under an MTP `global.json` (ticket 12, owner decision):** a new Environment entry in
     `docs/limitations.md` ("Detectable: no"), and a short section in `docs/adopting-reach.md`
     under the exit codes. Reach unchanged.
   - Rows 5, 12 and 29: fixed in Reach by tickets 17, 18 and 19 (owner decisions above). Their
     notes in spec §7 say so.
4. **Dogfood fix.** `dnx` is a script the .NET SDK installer puts on `PATH`, from .NET 10. The
   hosted image's preinstalled SDK has `dotnet` on `PATH` but not `dnx` (the failing runs show
   `dnx: command not found` after a successful build). Both `dogfood.yml` and the documented recipe
   gain an `actions/setup-dotnet@v5` step with `dotnet-version: 10.0.x`, which installs the SDK
   and puts its directory on `PATH`. `WorkflowParityTests` passes with the two still
   byte-identical. Not checked on a runner, because CI runs only when the orchestrator pushes. The
   workflow will still fail before `0.1.0-alpha.1` is published, now at the package lookup. That is
   expected (spec §9.2) and does not block publishing. **The recipe's shell-loop claim is proven
   only once dogfood runs against the published package.**
5. **`readme-only.patch` re-saved** with `apply` then `save --force` (the hunk header moved from
   `-32` to `-51`, where the README now has it). Row 14 unchanged.

**Local checks (Windows, Release):** `Reach.slnx` builds with `-warnaserror` (0 warnings) and
`dotnet format --verify-no-changes` is clean. Full suite: 687 passed, 0 failed. Release-gate run
(ticket 09's filter, every acceptance test except `Batch=known-gap`): 27 passed, 0 failed. That
is the 25 gate rows, `ExampleReportTests` and `ReachDirectorySurvivalTests`, with the four
known gaps left out.

**CI** (the latest run, for `5164600`, the `acceptance-layer` head this ticket branched from):
<https://github.com/LeeSanderson/dotnet-reach/actions/runs/45236183344>, both legs
(`ubuntu-latest`, `windows-latest`) green. The same push's dogfood run failed with
`dnx: command not found`, which this ticket fixes.

**Open for the owner.** Status stays `ready-for-human` until these two are decided:

- **Row 18 selects `Ledger.Dunning.Tests.ReminderSenderTests`, which spec §7.2 says it must not.**
  Api is widened whole, and the reverse walk from `WebhookChannel.Send` reaches Dunning through
  `ReminderSender`'s `IReminderChannel.Send` call (row 26's gap shape). Choices: (a) reshape
  Ledger so `Ledger.Api` holds no `IReminderChannel` implementer, for example by moving
  `WebhookChannel` into a project of its own that Api's tests still use, so row 26 still holds; (b) amend §7.2 row 18 to
  include `ReminderSenderTests`, saying why. **Recommendation: (a).** Row 18's point is what is
  *not* selected, and standing rule 4 says no row's patch reaches another row's gap shape. (b)
  makes the gate claim depend on a known gap. (a) is a sample change, and Reach stays unchanged.
- **A `Directory.Build.props` inside a generator's directory widens only the generator, so it
  selects nothing.** It is the same under-selection shape as the old row 12. Choices: (a) fix Reach,
  so rule 2 routes a props file inside a generator to its consumers, as ticket 18 did for every
  other file; (b) add a limitations entry and leave Reach as it is for the alpha. **Recommendation:
  (a)**, as a small follow-up ticket like 18. It is under-selection, which the owner has fixed each
  time so far (rows 5, 12, 29). If the alpha must not wait, take (b) now and (a) after it.
  `docs/limitations.md`'s generator entry says "whatever changed in it" and stays as it is until
  this is decided.
