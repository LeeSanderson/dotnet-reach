# Exit-path CLI tests

Status: resolved
Depends on: —
Spec: [§10.4](../../acceptance-layer/spec.md#104-tests-in-other-suites), [§9.2](../../acceptance-layer/spec.md#92-claim-to-suite)

## Goal

The claims about what Reach writes, and does not write, on each failing exit path, proven by CLI
tests.

## Scope

New tests, beside `Cli/CommandLineTests.cs` (one class for the exit paths is fine):

| Claim | Test |
|---|---|
| Exit 70 on an internal error | make the pipeline throw, assert exit 70 |
| A report is written on exits 2, 4, 5 and 70 | one test per exit code, each asserting `report.json` exists and has outcome `failed` |
| `summary` is absent when the run failed before rendering | assert on the exit-4 and exit-70 reports (or whichever fail before rendering) |
| `envelope.baseline` is absent on exit 4 | on the exit-4 report |
| `envelope.head` is absent when git cannot be read | a run where git cannot read the head |
| `selection-delivered-out-of-band` | add the assertion to the existing long-selection CLI test |
| `.reach/` files remain after the process exits | **one out-of-process smoke test**: run the built CLI as a child process, let it exit, assert the report and side-car files exist |

- **Exit 70 needs the pipeline to throw.** Use the existing `IProcessRunner` port
  (`Fixtures/ScriptedProcessRunner.cs`) to make a process call throw, rather than adding a seam
  (M1 standing rule: no interface for one implementation). If no path through the port reaches the
  catch-all, record why in this ticket's comments and choose the smallest change that does.
- The out-of-process test may be slow. Put it where the acceptance step runs it
  (`Reach.Tests.Acceptance` namespace, or a filter the CI steps include). Say which in the comments.
- Exit 2 here is the report-on-failure claim. Row 28 (ticket 07) is the end-to-end proof of exit 2
  itself. Do not duplicate it.

## Acceptance criteria

- Each row above has a named test that fails if the claim stops being true.
- No production behaviour changes, unless exit 70 is unreachable from a test without one (then the
  change is the smallest that works, and is recorded).

## Out of scope

Report-writer unit tests (ticket 14).

## Comments

**Done.** Tests, by row:

| Claim | Test |
|---|---|
| Exit 70 | `Cli/ExitPathTests.A_pipeline_that_throws_is_exit_70` |
| Report on 2, 4, 5, 70; `summary` absent | `Cli/ExitPathTests.Exit_2_…`, `Exit_4_…`, `Exit_5_…`, `Exit_70_writes_a_failed_report_with_no_summary` (all four stop before rendering, so each asserts both) |
| `envelope.baseline` absent on exit 4 | `Cli/ExitPathTests.Exit_4_writes_no_baseline_into_the_envelope` |
| `envelope.head` absent when git cannot read it | `Cli/ExitPathTests.A_head_git_cannot_read_is_absent_from_the_envelope` (real git, `rev-parse HEAD` fails) |
| `selection-delivered-out-of-band` | assertion added to `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file` and `A_long_selection_on_a_host_with_no_channel_is_chunked`. There was no long-selection *CLI* test; these renderer tests are the ones §9.2 cites for delivery |
| `.reach/` files survive the process | `Acceptance/ReachDirectorySurvivalTests.The_report_and_side_car_files_remain_after_the_process_exits`: runs `dotnet Reach.Cli.dll select --no-build --runsettings …` as a child process over a compiled two-project repository with one selected test, then asserts `report.json` and the merged `.runsettings` side-car. It is in the `Reach.Tests.Acceptance` namespace, so the acceptance CI step runs it |

**Finding: exit 70 did not exist.** Nothing in Reach produced it. An exception no phase anticipated
fell through to System.CommandLine's default exception handler, which printed it and exited **1**,
a usage error, with **no report**. That contradicted `adopting-reach.md` (70: internal error) and
`report-schema.md` (a report on every outcome except exit 1), and it blamed the caller for a bug in
Reach. No path through the port could reach a catch-all, because there was none. The smallest
change that works, in `src/Reach.Cli/ReachCommandLine.cs`:

- `ReachCli` is now a class taking the `IProcessRunner` (the existing port), so a CLI test can make
  a process call throw. Passing it as an eighth parameter tripped S107; a constructor dependency is
  the better shape anyway. `Program` passes `new ProcessRunner()`.
- `SelectOrInternalErrorAsync` wraps the pipeline call: an exception becomes exit 70 with a message
  and a `failed` report (no baseline, head or summary, since the partial run is lost). The filter is
  `when (!cancellationToken.IsCancellationRequested)`, so a cancelled run still propagates. A
  type-test filter (`is not OperationCanceledException`) tripped S2221.

No other production behaviour changed.

**Note for whoever runs the suite:** a bare `dotnet test` at the root refuses to run, because
`Reach.slnx` contains the Contoso fixture's VSTest projects and `global.json` pins
Microsoft.Testing.Platform. This was already true before this ticket. The suite runs with
`dotnet test --project tests/Reach.Tests/Reach.Tests.csproj`.
