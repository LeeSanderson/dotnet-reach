# Adopting Reach

Wiring Reach into a pipeline. Everything here is what you need; nothing here is background.

**Adoption cost for this release is judged, not measured.** There is no harness timing how long
this takes, and that is deliberate — the correctness bargain in the
[limitations register](limitations.md) *depends* on being enforced, so it became a build failure;
nothing rides on adoption cost that way, and a harness for it would spend the budget in the wrong
place. If this page costs you more than an hour, that is a bug in the page.

## Install

```bash
dnx dotnet-reach@0.1.0-alpha.1 select --help
```

**The version pin is a requirement, not a preference.** `dnx` with a bare package id will not
find a prerelease, and fails with a misleading *"not found in NuGet feeds"*. Pin it until there
is a stable release.

Do not install with `-g`. A globally installed tool is a version nobody can see in the
repository, which is the opposite of what a pipeline wants.

## The one line that makes it safe

> ### If Reach exits non-zero, run the whole suite or stop the build.

An empty selection exits `0`. Non-zero means *do not trust my answer* — so a pipeline that
honours this line cannot lose a test to a Reach failure, whatever the failure is.

## Exit codes

| Code | Meaning | What to do |
|---|---|---|
| `0` | success — **including an empty selection** | run what the report names; if it names nothing, run nothing |
| `1` | usage error — **the one case that writes no report** | fix the arguments |
| `2` | build failed | the build already failed; stop |
| `3` | an assembly is missing, or discovery could not choose between candidates | see the message — it names the candidates and the option that settles it |
| `4` | the baseline could not be resolved, including a shallow clone | see the message — it prints the line to add |
| `5` | source and binary do not correspond | build, or drop `--no-build` |
| `70` | internal error | a bug in Reach |

### Exit 8 from the test run

Reach never emits an invocation whose filter matches nothing: an empty selection emits no
command at all, every chunk carries at least one test by construction, and a multi-targeted
project pins its framework. So with current Microsoft.Testing.Platform hosts, **exit 8 from a
command Reach emitted means the rendering matched nothing while the report claimed tests were
selected — a Reach bug and nothing else.** Please report it.

**Reach never recommends `--ignore-exit-code 8`,** and nor should you. It does not merely
suppress the signal: the zero-match module's line changes from *Zero tests ran* to *passed* and
the annotation disappears from the summary entirely. It also stopped working on the .NET 11 SDK,
where zero-match handling moved to a run-level verdict — which is why emitting no command at all
is the only host- and SDK-version-independent answer.

Two ways to see exit 8 from a correct invocation, both your own choice:
`--zero-tests-policy strict` when every selected test in a project is skipped, and a rebuild
between Reach's run and the test step.

## CI

Reach needs the full history: it resolves a baseline with `git merge-base`, and
`actions/checkout` fetches a single commit by default. **This is the likeliest thing to go wrong
on your first run**, and it is one line.

### GitHub Actions — the complete recipe

**This block is not an illustration — it is the workflow this repository runs on its own pull
requests**, `.github/workflows/dogfood.yml`, and a test in Reach's suite fails the build if the
two differ by one character. Copy it as `.github/workflows/tests.yml`.

<!-- reach:dogfood-workflow -->
```yaml
name: Tests

on:
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v5
        with:
          # Without this, git merge-base has no history to walk and Reach exits 4.
          fetch-depth: 0

      - name: Build
        run: dotnet build -c Release

      - name: Select the tests this change could affect
        run: dnx dotnet-reach@0.1.0-alpha.1 select -c Release --no-build

      # An empty selection emits no command, so the loop runs nothing. A failing test still
      # fails the step: `eval` inside a loop does not propagate on its own.
      - name: Run the selection
        run: |
          status=0
          while read -r command; do
            [ -z "$command" ] && continue
            echo "$command"
            eval "$command" || status=1
          done <<< "$(jq -r '.entries[].invocations[] | @sh' .reach/report.json)"
          exit $status

      - name: Run the whole suite
        run: dotnet test -c Release --no-build

      # The gap between what Reach selected and what the whole suite ran is the number worth
      # looking at, so keep the report whatever happened above.
      - name: Keep the report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: reach-report
          path: .reach/report.json
          if-no-files-found: ignore
```
<!-- /reach:dogfood-workflow -->

That recipe runs **both** the selection and the full suite, and only the full suite gates. That
is deliberate for now: gating on a selection nobody has measured is the unproven narrowing this
tool exists not to do. Running both is a miniature shadow mode, and the gap between them is the
first real datapoint.

`jq` is preinstalled on GitHub's hosted runners. The `@sh` filter quotes each argv element, which
is why `eval` is safe here and why Reach emits argv vectors rather than shell strings in the first
place.

### Every other provider

One row each, rather than five copies of three steps. An unverified recipe is worse than a table
row, because it looks authoritative.

| Provider | Fetch the whole history | Reach detects it from |
|---|---|---|
| GitHub Actions | `fetch-depth: 0` on `actions/checkout` | `GITHUB_BASE_REF` |
| Azure DevOps | `fetchDepth: 0` on `checkout: self` | `SYSTEM_PULLREQUEST_TARGETBRANCH` |
| GitLab CI | `GIT_DEPTH: 0` in `variables` | `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` |
| Bitbucket Pipelines | `clone: depth: full` | `BITBUCKET_PR_DESTINATION_BRANCH` |
| Jenkins multibranch | turn off *Shallow clone* in the job's Git behaviours | `CHANGE_TARGET` |
| TeamCity | (its default checkout is deep) | **nothing — pass `--base` explicitly** |

TeamCity is not detected on purpose: `teamcity.pullRequest.target.branch` is a configuration
parameter, and TeamCity passes only `env.`-prefixed parameters to the build process.

**Reach never reads a *source* branch variable** — `GITHUB_HEAD_REF`,
`System.PullRequest.SourceBranch`, `Build.SourceBranch` and their equivalents. A source branch
resolves at or ahead of `HEAD`, which produces an empty change set and a green pipeline: the one
detection bug that under-selects rather than over-selects.

## The switches whose behaviour surprises

### `--no-build` detects *more*, not less

The mode that trusts you more is the mode that catches more. Reach compares each source file
against the checksum the compiler recorded for it, in **both** modes — and in default mode the
build has just recompiled your change, so every checksum agrees. Under `--no-build` a file you
edited since the last build fails the check and the run exits 5.

This is not a timestamp heuristic. Git does not preserve timestamps, so checking out an older
commit onto a warm agent can leave source *older* than the binaries beside it, and MSBuild will
correctly conclude nothing needs rebuilding while the binaries belong to a different commit.

### `--runsettings <path>` merges your filter, it does not use your settings

Read it as *"merge Reach's filter into this runsettings file and write the result"*. Reach runs
no tests, so it never uses the settings for anything. If your file already carries a
`<TestCaseFilter>`, Reach **refuses to render a filter for that project and runs it in full** —
a pre-existing filter is AND-ed with Reach's, and yours is typically an exclusion, so the
intersection would be strictly smaller than the selection and undetectably so.

### `--output` is the build's, never Reach's

`-o|--output` and `--artifacts-path` describe where your **build** put its output; Reach uses
them to narrow its scan. Where *Reach* writes is `--report` and `--report-dir`.

Give those switches to Reach directly. Forwarding one after `--` is refused with exit 1: it
would change the output layout through a channel Reach never read.

### Moving a type between namespaces costs more than moving it between directories

A directory-only move is found on both sides and widens that type. A **namespace** move is a
delete plus an add, and widens the whole assembly — because a namespace is part of the metadata
name, so the move changes what runtime binding sees: serialization discriminators,
convention-based registration, `Type.GetType`. Those are gaps Reach cannot see into, so it takes
the wide answer.

### `.reach/` is yours to clean up

Reach writes `report.json` and every response file into `.reach/` under the working directory,
and **never deletes them** — your runner reads those files in a *later* pipeline step, so
removing them on exit would break the contract Reach just published. It writes a `.gitignore`
containing `*` there on first use, so you will not commit a report by accident.

## Next

- [The report schema](report-schema.md) — every field, every notice code, and how to read a
  selection you do not believe.
- [The limitations register](limitations.md) — what Reach cannot do, and which way each gap
  fails.
