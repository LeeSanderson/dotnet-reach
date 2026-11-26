# Frameworks: rows 7 and 8, the example report, and the end of `tests/fixtures/`

Status: resolved
Depends on: 06, 07, 08, 11
Spec: [§2.4](../../acceptance-layer/spec.md#24-frameworks-was-contoso), [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) rows 7–8, [§8](../../acceptance-layer/spec.md#8-migrating-the-contoso-acceptance-tests), [§5.5](../../acceptance-layer/spec.md#55-fixturesolution-becomes-samplecopy)

## Goal

Contoso becomes Frameworks, the last old acceptance tests are replaced, and `tests/fixtures/` and
the old `FixtureSolution` API are gone.

## Scope

**The sample** (spec §2.4):

- `git mv tests/fixtures/Contoso tests/samples/Frameworks`, and rename everything to
  `Frameworks.*`: solution, projects, namespaces. Move the `Reach.slnx` entries with it.
- Remove `Contoso.Core`'s multi-targeting (now single `net10.0`) and the generator project. Rows 9,
  10 and 12 live on Basics.
- Test projects named by dialect: `Frameworks.Tests.XunitV2`, `.XunitV3` (`xunit.v3` 3.x),
  `.XunitV3_4` (`xunit.v3` 4.0.0), `.NUnit`, `.MSTest`, `.Unrecognised`. Add the three that do not
  exist (`XunitV2`, `XunitV3` 3.x, `MSTest`). Every one calls the same production code.
- Keep the NUnit parameterised test and the `MyTest`/`MyTest2` pair (`MyTest` reaches `Tax`,
  `MyTest2` reaches `Untouched`). Keep `Microsoft.NET.Test.Sdk` on the unrecognised project.
- Add `global.json` with the runner setting.
- Check that CI's test steps still never run these projects directly (VSTest projects would break a
  testing-platform `dotnet test`).

**Rows** in `Acceptance/Frameworks/FrameworksTests.cs`:

| Row | Patch | Sections beyond the required four |
|---|---|---|
| 7 | the `Tax` change | `WillRun` per dialect (NUnit's exceeds its selection), `Notices` (`nunit-filter-overmatch`, `dialect-over-selects`, `whole-project-fallback`, and whatever else Frameworks raises as standing) |
| 8 | any change reaching the unrecognised project | `RunInFull` = `Frameworks.Tests.Unrecognised`, `Notices` |

**The example report** (spec §8.2): `ExampleReportTests` runs over Frameworks with **row 7's patch
file**. Regenerate `docs/example-report.json` and update the walk-through in
`docs/report-schema.md` to the new names and project count. `test-runner-not-configured` leaving the
example is expected; the code stays in the schema index. Check the regenerated example carries
`changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence`, `runnerHost`,
`framework` and `dialect`. Record in this ticket's comments which are present, because ticket 15
depends on it.

**The end of the old world:**

- Delete `Acceptance/AcceptanceTests.cs`. Its last two tests
  (`The_NUnit_filter_renders_with_contains_and_reports_the_extra_it_will_run`,
  `An_unrecognised_framework_runs_in_full_with_an_unknown_total`) are replaced by rows 7 and 8 here.
  **Before deleting, check that every other test in it has been deleted by tickets 02, 06, 07, 08
  and 11.** If one has not, its replacement has not landed. Stop.
- Delete `tests/fixtures/`.
- Remove `FixtureSolution`'s old API (`WriteFile`, `Touch`, `Rebuild`, the Contoso-specific
  members) and rename the class if ticket 02 did not.
- Leave the in-memory tests that use "Contoso" as a made-up name alone.

## Acceptance criteria

- `tests/fixtures/` and `AcceptanceTests.cs` no longer exist. Nothing references `Contoso` except
  unrelated in-memory test strings.
- Rows 7 and 8 pass on Windows and Linux. Row 7's `WillRun` shows NUnit running more than it
  selects.
- `ExampleReportTests` passes over Frameworks, and the regenerated example and walk-through are
  committed.
- Frameworks builds clean in `Reach.slnx`, and CI's test steps do not run its projects.

## Out of scope

The walk-through parity test (ticket 15).

## Comments

- **Done.** `tests/fixtures/Contoso` moved (`git mv`) to `tests/samples/Frameworks`, renamed
  `Frameworks.*`: `Frameworks.Core` (single `net10.0`, namespace `Frameworks.Core`, no generator)
  and six test projects, `XunitV2` (`xunit` 2.9.3), `XunitV3` (`xunit.v3` 3.2.2), `XunitV3_4`
  (4.0.0), `NUnit` (`MyTest`/`MyTest2` kept), `MSTest` (3.11.0) and `Unrecognised` (keeps
  `Microsoft.NET.Test.Sdk`). Each one calls `Invoice.Total` and `Invoice.Untouched`. The sample has
  `global.json`, a README, and is in `Reach.slnx`. Rows 7 and 8 are in
  `Acceptance/Frameworks/FrameworksTests.cs`, with patches saved by the tool:
  `tax-rounds-half-up` (row 7, and `ExampleReportTests` uses it through `FrameworksTests.TaxChange`)
  and `unrecognised-check-message` (row 8, which edits only the unrecognised project's own check).
  Both passed on the first run, against expectations written by hand. `AcceptanceTests.cs` is
  deleted (only its two tests were left), and so are `tests/fixtures/` and `SampleCopy`'s old API
  (`Build`, `WriteFile`, `ReadFile`, `Rebuild`, `SelectAsync`, `ContosoDirectory`).
  `ExampleReportTests` now runs on the harness (`Create`/`ApplyChanges`/`RunTwiceAsync`).
  `docs/example-report.json` was regenerated with `REACH_UPDATE_EXAMPLE=1`, and the walk-through
  in `docs/report-schema.md` was updated. Release `-warnaserror` build of `Reach.slnx` is clean.
  Full suite: 664 passed, 0 failed (Windows, Debug). Linux not run locally.
- **Row 7 notices:** `baseline-resolved`, `baseline-is-head`, `nunit-filter-overmatch`,
  `whole-project-fallback`, `dialect-over-selects`. **Row 8:** the same four standing codes, but
  not `dialect-over-selects`. So both NUnit and the unrecognised project raise their notice on every
  run. Row 8's outcome is `selected` with nothing selected and only `RunInFull`. `WillRun` in row 7
  is 1 for each `=` dialect and 2 for NUnit.
- **For ticket 15: the regenerated example has all seven fields.** `changeSources`, `buildMode`
  (`no-build`), `forwardedBuildArguments` (`[]`) and `correspondence` (`verified`) are in the
  envelope. `runnerHost` (`dotnet-test`) is on every entry. `framework` and `dialect` are on the five
  recognised entries, equal (`mstest`, `nunit`, `xunit-v2`, `xunit-v3`, `xunit-v3-4`), and both are
  missing from the unrecognised one. `test-runner-not-configured` has left the example, as expected.
- **Finding (spec §8.2): the solution has seven projects, not eight.** One production project plus
  six test projects. The walk-through says seven.
- **Finding (Reach): no notice says the VSTest commands cannot run.** `global.json` selects
  Microsoft.Testing.Platform, so the .NET 10 SDK refuses `dotnet test` on any VSTest project ("All
  projects must use that test runner"). I checked this by running an emitted NUnit command in the
  sample. Reach still emits `dotnet test <proj> --filter …` for `XunitV2`, `NUnit`, `MSTest` and
  `Unrecognised` with no notice. It is the reverse of `test-runner-not-configured`. Pinned, not
  changed. The example report shows these commands.
- **Bare `dotnet test` at the root still fails, now for this reason:** the SDK lists the four
  Frameworks VSTest projects and exits 1 before running anything. CI's test steps name
  `tests/Reach.Tests/Reach.Tests.csproj` only, so they are not affected (checked
  `ci.yml`, `dogfood.yml`, `release.yml`).
- **Process note:** `patch.cs save` diffs against `HEAD`. Running it before the moved sample was
  committed turned the whole sample into one patch and then reverted (deleted) it. I recovered it
  from that patch. Commit a new or moved sample before saving patches against it.
- Also renamed "fixture projects" to "sample projects" in the matching comments in
  `docs/adopting-reach.md` and `.github/workflows/dogfood.yml`, and fixed the stale Contoso comment
  in `Directory.Build.props`.