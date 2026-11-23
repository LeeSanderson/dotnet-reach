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
