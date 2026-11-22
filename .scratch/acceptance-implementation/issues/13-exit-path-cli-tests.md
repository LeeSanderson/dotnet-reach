# Exit-path CLI tests

Status: ready-for-agent
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
