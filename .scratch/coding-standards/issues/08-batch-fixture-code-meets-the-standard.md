# 08: Batch: the fixture code meets the standard

**What to build:** Built in place as part of the main solution, the fixture solution raises no diagnostics under the coding standard, and the acceptance tests still find and edit the lines they expect. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

- Built in place, the fixture projects inherit the root build props, and with them the package. Fix their code, including `dotnet format`'s automatic fixes.
- The acceptance tests edit fixture files by **exact text replacement**. Wherever a fix rewrites a line one of those edits matches (e.g. S109 on the `100` in the `Tax` expression), change the acceptance test in the **same commit**.
- The fixture must still model what it models: a multi-targeted core project, a source generator, an xUnit v3 project, an NUnit project, and a deliberately framework-less test project. A fix that would remove one of those shapes needs the maintainer's approval instead.
- **No suppression without the maintainer's explicit approval for that rule.** Record any request under `## Comments`.

**Blocked by:** 03 (the fixtures join the main solution), 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [x] Zero diagnostics in the fixture projects when built in place
- [x] `dotnet format --verify-no-changes` reports nothing for the fixture projects
- [x] Every exact-text edit in the acceptance tests still matches its fixture line
- [x] The acceptance tests pass (the temporary-repository build stays plain)
- [x] No suppressions added unless approved (approval quoted under `## Comments`)

## Comments

Built in place, the fixture raised 13 diagnostics: MA0211 ×2, IDE0130 ×3, SA1649 ×1, S109 ×3,
S2325 ×2, S3400 ×1, S2339 ×1. Each family is its own commit. No suppressions were added.

- **CRLF regression (first commit).** `Touch` now translates `\n` in its find and replace text
  to the line ending the fixture file was checked out with. With that,
  `Fact_added_to_an_existing_method_selects_it` passes on a fully renormalised CRLF worktree.
- **IDE0130**: each project's `RootNamespace` is set to the namespace its code already uses
  (`Contoso`, `Contoso.Generation`, `Contoso.Tests`). The namespaces were not renamed, because
  the expected test names in the acceptance tests and the committed `docs/example-report.json`
  depend on them.
- **S2325 / S3400**: `Invoice` is now a static class. `Untouched()` returned the constant 0, so
  it is now `Untouched(int net) => net`. It is still a method that only its own tests reach.
- **S2339 (worth a look):** `StandardRate` is now an `internal const`, not a `public const`.
  The rule's suggested fix, a static read-only property, would remove the const that makes a
  rate change a recompilation trigger. `RecompilationWidening` widens on a changed const of any
  visibility, and `Tax` still inlines it. Nothing in the fixture consumed it across an assembly
  boundary before this change either.
- **A second exact-text edit**: `ExampleReportTests` also edits `Invoice.cs` by exact text, and
  it had no assertion that the text matched. Its needle is updated in the S109 and S2325 commits,
  and it now asserts the match. The committed example report is unchanged byte for byte.
- **Stale doc, not changed here:** `tests/fixtures/Contoso/README.md` still says the fixture
  "is never built in place". That stopped being true with ticket 03.

Verified: the fixture solution builds with 0 warnings and 0 errors under the default
warnings-as-errors. `dotnet format tests/fixtures/Contoso/Contoso.slnx --verify-no-changes
--severity info` exits 0. The full Reach.Tests suite passes, 521 of 521 (run with the
documentation rules that batch 05 owns switched off on the command line only).
