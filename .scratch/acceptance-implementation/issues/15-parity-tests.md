# Parity tests: the notice index, the walk-through and the help

Status: ready-for-agent
Depends on: 04, 05, 12
Spec: [§10.3](../../acceptance-layer/spec.md#103-help-and-the-parity-tests), [§9.2](../../acceptance-layer/spec.md#92-claim-to-suite)

## Goal

The doc prose that can drift from what Reach does is pinned to it by tests, like
`RegisterParityTests` and `WorkflowParityTests`.

## Scope

Three parity tests in `Reporting/`, each with a companion test showing that one changed value fails
it (the pattern `WorkflowParityTests.One_changed_character_in_the_documented_recipe_fails_the_comparison`
uses):

1. **The notice index.** The codes in `docs/report-schema.md`'s notice index, and the kind each is
   listed under, equal `NoticeCatalogue`'s. Missing or extra in either direction fails, naming the
   code.
2. **The walk-through.** Every value the schema's walk-through quotes from
   `docs/example-report.json` (entry indexes, counts, notice codes on named entries, the project
   count in the prose) equals the example. Choose how the prose marks a quoted value so the test can
   find it. A small, consistent convention in the Markdown is fine. Explain it in a comment at the
   top of the walk-through.
3. **The help options.** The option names in `select --help` (the golden file, or the parser
   itself) equal the options `docs/adopting-reach.md` and `docs/report-schema.md` describe. Names
   only; descriptions stay hand-kept.

**The four envelope fields.** `changeSources`, `buildMode`, `forwardedBuildArguments` and
`correspondence` are proven by `ExampleReportTests` if the regenerated example carries them (ticket
12 records which it does). For any it lacks, add a report-writer test asserting the field as the
schema describes it.

## Acceptance criteria

- Three parity tests and their three fails-on-drift companions pass.
- Each envelope field is proven by the example or by a named test.
- If a parity test finds drift when it is first written, fix the docs, not the test, and list what
  was fixed in this ticket's comments.

## Out of scope

Parity for `limitations.md` beyond what `RegisterParityTests` already checks. The gate audit
(ticket 16) checks the register's "Detectable" lines by hand.
