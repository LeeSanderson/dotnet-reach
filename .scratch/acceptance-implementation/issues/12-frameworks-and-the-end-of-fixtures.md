# Frameworks: rows 7 and 8, the example report, and the end of `tests/fixtures/`

Status: ready-for-agent
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
