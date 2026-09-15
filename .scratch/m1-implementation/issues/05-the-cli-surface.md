# The CLI surface

Status: resolved
Depends on: 03, 04
Spec: [§4](../../walking-skeleton/spec.md#4-the-command-line), [§14.2](../../walking-skeleton/spec.md#142-exit-codes)

## Goal

`dotnet reach select` with its full option surface, its exit codes, and the `.reach/`
directory — wired to the phases that exist so far and returning honest exit codes for the rest.

## Scope

```
dotnet reach select [<SOLUTION|PROJECT>] [options] [-- <dotnet build args>]
```

One verb, one positional, eleven options, built on `System.CommandLine` 2.0.x — stable since
.NET 10, zero package dependencies, and what the `dotnet` CLI itself is built on.

| Option | Default |
|---|---|
| `<SOLUTION\|PROJECT>` positional | discovered in the working directory (ticket 04) |
| `--base <ref>` | auto-detected (ticket 03) |
| `-c\|--configuration <name>` | the SDK's default |
| `-o\|--output <dir>` | unset |
| `--artifacts-path <dir>` | unset |
| `--no-build` | off |
| `--report <path\|->` | `.reach/report.json` |
| `--report-dir <dir>` | `.reach` |
| `--paths` | off |
| `--list-unselected` | off |
| `--runsettings <path>` | unset |
| `-v\|--verbosity <level>` | `normal` |
| `--no-color` | auto-detected |

**The verb is required, and bare `dotnet reach` prints help and exits 1.** Two reasons, both
load-bearing. The default mode invokes `dotnet build`, so the one invocation a newcomer is
guaranteed to try must not trigger a multi-minute build and write files into their repository.
And a bare invocation writes no report, so exiting 0 would hand a mis-wired pipeline "success"
plus a missing report, and a consumer looping over `invocations` would run nothing.
**`--help` asked for explicitly exits 0.** The adoption criterion is read as `dotnet reach
select` needing no further arguments, not as the verb being omissible.

**Option spellings mirror `dotnet build` exactly** — same long names, same short forms, same
semantics — so a pipeline author transfers what they already know and Reach never invents a
synonym for a concept the SDK has named. `--artifacts-path` is hyphenated and has **no short
form** on any `dotnet` command, so neither does Reach's. **That rule governs spelling, not
resolution semantics**: where a resolution rule could narrow scope, the correctness rule
outranks the convention (ticket 04 is the first application).

**`--` forwards verbatim to `dotnet build`, except what Reach owns.** Without a passthrough
every adopter who builds with `-p:` properties, `--no-restore` or a binlog is pushed onto
`--no-build` permanently, quietly making the documented default the minority path. **Reach
scans the forwarded tokens for `-o`, `--output`, `--artifacts-path` and `-c` and refuses with
exit 1**, saying they must be given to Reach directly: a forwarded `-o` changes the output
layout through a channel Reach never read. The forwarded argv goes in the report envelope so a
surprising run stays reproducible.

**Reach owns one directory**: `.reach/` under the **working directory**, not beside the
solution, holding `report.json` and every response file and testlist. `--report <path|->` moves
the JSON alone; `--report-dir <dir>` moves the whole directory. `--output` is reserved for
build output and can never mean "where Reach writes". Reach writes `.reach/.gitignore`
containing `*` on first use, so the first adopter to run it locally does not commit a report —
a judgement call rather than a precedent-backed one, since the SDK ships `artifacts/` as a line
in the `dotnet new gitignore` template instead, and Reach's directory is covered by no such
template. **Reach never cleans the directory up**: the runner reads those files in a *later*
pipeline step.

**Streams**: summary to stdout, notices and logs to stderr; `--report -` sends JSON to stdout
and moves the summary to stderr. `-v|--verbosity` mirrors the SDK's
`quiet|minimal|normal|detailed|diagnostic`. Colour auto-detected, `--no-color` and `NO_COLOR`
honoured.

**Exit codes**: `0` success (including an empty selection) · `1` usage error, the one case that
writes **no report** · `2` build failed · `3` assembly missing or discovery failed · `4`
baseline unresolvable · `5` correspondence failed · `70` internal error.

**No option may narrow scope.** No test-project include/exclude, no target-framework
restriction, no `--no-untracked`. A switch whose only possible effect is under-selection is a
poor use of the option budget. The chunking ceiling is detected per platform and is never a
flag. **No notice-suppression flag and no schema-version flag** in M1.

**`--runsettings` is documented by its effect** — "merge Reach's filter into this runsettings
file and write the result" — because the name is a foot-gun: someone will pass it expecting
Reach to *use* their settings for a test run Reach never performs.

## Acceptance criteria

- Bare `dotnet reach` prints help and exits 1; `dotnet reach --help` exits 0.
- Each layout switch appearing after `--` exits 1 with a message naming the Reach option to use
  instead.
- Other tokens after `--` reach the build adapter verbatim and appear in the envelope.
- `--report -` moves the summary to stderr and writes valid JSON to stdout.
- `.reach/.gitignore` containing `*` is written on first use and not rewritten afterwards.
- `--report-dir` relocates side-car files as well as the report.
- `NO_COLOR` and `--no-color` both suppress colour; a redirected stdout does too.
- Every exit code is reachable from a test, with exit 1 asserted to write no report file.
- A golden test over `--help` output, so an accidental option rename is caught.

## Out of scope

The phases themselves. Where a phase does not exist yet, wire the option and return the exit
code; do not stub behaviour that later tickets will replace.

## Comments

**Implemented.** `Reach.Cli/ReachCommandLine.cs` builds the tree and maps `ParseResult` to
`SelectRequest`; `ReachCli.RunAsync` is the whole entry point and takes its streams, working
directory, environment lookup and redirect flag as parameters, so the CLI is testable in
memory with no process. `Reach.Core` gains `SelectRequest`, `Verbosity`, `ReachPipeline`, and
`Output/` (`ForwardedBuildArguments`, `ReachDirectory`, `ReportDestination`, `Streams`,
`ColourSupport`).

**Reach splits the argument vector at `--` itself, before `System.CommandLine` sees it.** This
is not a preference. Left to the parser, `reach select -- --no-restore` parses `--no-restore`
as the *positional target* and forwards nothing, because the zero-or-one argument is greedy.
Verified against `System.CommandLine` 2.0.0 before the split was written, and there is a named
test for it.

**Three behaviours the parser already gets right**, confirmed rather than built: bare
`dotnet reach` prints "Required command was not provided." plus help and exits 1; `--help`
exits 0; an unknown option is a parse error and exits 1. `TreatUnmatchedTokensAsErrors` stays
at its default `true` — which only works *because* Reach removes the `--` tail first.

**`--configuration` is refused after `--` as well as `-c`.** The ticket names `-c`; the long
form is the same switch and refusing only the short one would be a hole a `dotnet build`
habit walks straight through. `--output=x` and `--output:x` spellings are refused too.

**`EnvironmentLookup` moved** from `Reach.Baselines` to the root `Reach` namespace: colour
detection and the CLI read the environment as well as the detection ladder.

**`ReachPipeline` puts every usage check before it touches `.reach/`**, because exit 1 is the
one case that writes no report. The order is forwarded-argument refusal, target discovery,
analysis scope, then the directory, then baseline resolution. A test asserts a usage error
leaves no `.reach/` directory at all.

**A golden test covers both help texts** (`tests/Reach.Tests/Cli/Golden/`). The executable name
is normalised to `reach` because `RootCommand.ExecutableName` is the test host under the
suite. `REACH_UPDATE_GOLDEN=1` rewrites the files when the change is intended; the golden path
is found with `[CallerFilePath]`, so no build plumbing is involved.

**Two acceptance criteria are only half-met, and both halves wait on a later ticket.**

- *"`--report -` … writes valid JSON to stdout"* — the stream routing is implemented and
  tested (`Streams.For` puts the report on stdout and the summary on stderr); there is no JSON
  to write until the report writer lands.
- *"Every exit code is reachable from a test"* — 0 (`--help`), 1, 4 and 70 are reachable now.
  2 (build failed), 3 (assembly discovery) and 5 (correspondence) become reachable with their
  phases. `ExitCodeTests` pins the whole closed set and its numbering meanwhile.

Where a phase does not exist, the pipeline returns exit 70 with a message naming what it did
resolve. That is honest rather than stubbed, and each later ticket replaces one line of it.
