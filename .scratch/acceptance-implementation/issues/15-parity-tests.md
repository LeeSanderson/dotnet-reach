# Parity tests: the notice index, the walk-through and the help

Status: resolved
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

## Comments

**Done.** Three parity tests in `tests/Reach.Tests/Reporting/`, each with its drift companions.
All pass. Full suite: 672 passed, 0 failed (Windows, Debug).

- **`NoticeIndexParityTests`**: `The_index_lists_every_code_Reach_can_emit_under_its_kind`.
  Companions: `One_renamed_code_in_the_index_fails_the_comparison` (reports the missing code and
  the extra one) and `A_code_under_the_wrong_kind_fails_the_comparison`. The index already matched
  `NoticeCatalogue`, so no drift there.
- **`WalkThroughParityTests`**: `Every_value_the_walk_through_quotes_is_the_example_s`. Companion:
  `One_changed_value_in_the_walk_through_fails_the_comparison`. The convention: HTML comments,
  invisible on GitHub. `<!-- example: PATH = VALUE -->` goes right after the value it pins.
  `<!-- example: PATH -->` goes on the line before a `json` block, and the block must then match
  the example at PATH (a subset match). There is a small path syntax (`[n]`, `[*]`, `[?key]`,
  `[?key=text]`, `count(...)`), and `…` matches anything inside a quoted string. A `json` block
  with no marker fails the test. The convention is explained in a comment at the top of the
  walk-through. 19 markers. Every value already matched the example.
- **`HelpOptionParityTests`**: `The_docs_describe_exactly_the_options_select_help_lists`.
  Companions: `An_option_the_docs_do_not_describe_fails_the_comparison` (uses
  `--suppress-notices`, so this also proves "notices cannot be suppressed") and
  `An_option_the_parser_does_not_take_fails_the_comparison` (uses `--list-unselected`). Help
  names are read from the golden file, which `HelpGoldenTests` keeps equal to the parser. Doc
  names are option-shaped tokens in backticks in the prose of the two docs. Fenced blocks are
  skipped. A named list holds the other tools' options that the docs cite (`-g`, `--prerelease`,
  `--project`, `--ignore-exit-code`, `--zero-tests-policy`).
- **Envelope fields.** All four (`changeSources`, `buildMode`, `forwardedBuildArguments`,
  `correspondence`) are in the regenerated example, so `ExampleReportTests` proves them. No
  report-writer test was added.

**Drift found and fixed in the docs:**

- **Help options.** Neither doc described `-c|--configuration`, `-v|--verbosity`, `--no-color`
  or `--help`. `-c` appeared only inside the recipe's YAML. `adopting-reach.md` now has a short
  paragraph that names them, at the top of "The switches whose behaviour surprises".
- **`dialect-over-selects` index row.** It claimed `data.projects`. Reach emits this notice with
  no `data`: the message names each entry and its two counts (`Renderer.cs`, and the example).
  The row now says that. Reach is unchanged.
