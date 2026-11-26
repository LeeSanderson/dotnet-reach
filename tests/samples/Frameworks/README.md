# Frameworks

A small **sample solution** for Reach's acceptance layer, whose point is its standing notices. It
carries rows 7 and 8 of the release gate and the example report. The rows are defined in
[the publishing spec](../../../.scratch/publishing/spec.md#3-the-release-gate), and the sample's design in
[the acceptance-layer spec, §2.4](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/spec.md#24-frameworks-was-contoso).

This folder is the baseline repository Reach reads in an acceptance test, so it holds the sample
and nothing else. The acceptance tests live in `tests/Reach.Tests/Acceptance/Frameworks/`, beside
the patches they apply. `ExampleReportTests` uses row 7's patch, so the example and a gate test
cannot drift apart.

| Project | What it is here for |
|---|---|
| `src/Frameworks.Core` | the production code every test project calls, single `net10.0` |
| `tests/Frameworks.Tests.XunitV2` | xUnit v2 (`xunit` 2.x), on VSTest |
| `tests/Frameworks.Tests.XunitV3` | `xunit.v3` 3.x |
| `tests/Frameworks.Tests.XunitV3_4` | `xunit.v3` 4.0.0, the dialect that changed the filter surface |
| `tests/Frameworks.Tests.NUnit` | the **`~` dialect**, with a parameterised test and a `MyTest`/`MyTest2` pair: `MyTest` reaches `Tax`, `MyTest2` reaches `Untouched`, and a filter naming `MyTest` matches both |
| `tests/Frameworks.Tests.MSTest` | MSTest, on VSTest |
| `tests/Frameworks.Tests.Unrecognised` | a test project on a framework Reach does not recognise; its `Microsoft.NET.Test.Sdk` reference is what makes it a test project |
| `global.json` | the testing-platform runner setting, and nothing else that changes the build |

Two notices are **standing**: every run raises them, whatever the patch. `nunit-filter-overmatch` comes from the NUnit project and `whole-project-fallback` from the
unrecognised one. That is why these shapes live here and not on Basics.

| Patch | Row |
|---|---|
| `Invoice.Tax` | 7: each dialect is recognised and renders its own filter; NUnit's `~` runs more than it selects |
| `InvoiceChecks` in the unrecognised project | 8: a project on an unrecognised framework runs in full, with an unknown total |

## Building it

The projects are in `Reach.slnx` under `/samples/`, so the main build holds them to the coding
standard. Their tests are never run by `dotnet test`; they run only through Reach, inside an
acceptance test, over a copy of the files git tracks here. Four of the test projects (xUnit v2,
NUnit, MSTest and the unrecognised one) are on VSTest, which a testing-platform `dotnet test`
refuses, so CI's test steps name `tests/Reach.Tests/Reach.Tests.csproj` and nothing else.
