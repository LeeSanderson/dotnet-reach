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
find a prerelease: it selects one only under `--prerelease`
([`dotnet tool exec`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec#options)),
and otherwise fails with a misleading *"not found in NuGet feeds"*
([the SDK's message](https://github.com/dotnet/sdk/blob/3572593e44eb27035e2faa9a46a9d6c850de6b69/src/Cli/dotnet/CliStrings.resx#L533-L535)).
Pin it until there is a stable release.

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

**Reach never recommends `--ignore-exit-code 8`,** and nor should you. It returns that exit code
as `0`
([Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting#ignore-specific-exit-codes)),
which hides the one signal that a rendering matched nothing. It is also not how the .NET 11 SDK
decides: there, `dotnet test` decides the zero-tests verdict once for the whole run, from the
aggregated results
([`dotnet test`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp#whole-run-and-per-module-minimums),
[microsoft/testfx#7457](https://github.com/microsoft/testfx/issues/7457)). That is why emitting no
command at all is the only host- and SDK-version-independent answer.

Two ways to see exit 8 from a correct invocation, both your own choice:
`--zero-tests-policy strict` when every selected test in a project is skipped
([MTP options](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options)),
and a rebuild between Reach's run and the test step.

## CI

Reach needs the full history: it resolves a baseline with `git merge-base`, and
`actions/checkout` fetches a single commit by default
([its README](https://github.com/actions/checkout#readme)). **This is the likeliest thing to go wrong
on your first run**, and it is one line.

### GitHub Actions — the complete recipe

**This block is not an illustration — it is the workflow this repository runs on its own pull
requests**, `.github/workflows/dogfood.yml`, and a test in Reach's suite fails the build if the
two differ by one character. Copy it as `.github/workflows/tests.yml`, and replace
`tests/Reach.Tests/Reach.Tests.csproj` in the two steps that name it with your own solution or
test project. Or drop it (with `--project` before it), and both steps use the solution in the
working directory.

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

      # Reach's own solution also holds the sample projects its tests build, so this names its
      # one test project. Put your solution or test project in its place, here and in the whole
      # suite below. Without one, both find the solution in the working directory.
      - name: Select the tests this change could affect
        run: dnx dotnet-reach@0.1.0-alpha.1 select tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build

      # An empty selection emits no command, so the loop runs nothing. Two things the obvious
      # one-liner gets wrong: `eval` inside a loop does not propagate failure, so a failing
      # test leaves the step green; and a child that reads stdin swallows the commands still
      # queued behind it.
      - name: Run the selection
        run: |
          status=0
          while read -r command; do
            [ -z "$command" ] && continue
            echo "$command"
            eval "$command" < /dev/null || status=1
          done <<< "$(jq -r '.entries[].invocations[] | @sh' .reach/report.json)"
          exit $status

      # The full suite is what gates, so it runs even when the step above failed. Without this
      # the one run where the two disagree is the one that never compares them.
      - name: Run the whole suite
        if: ${{ !cancelled() }}
        run: dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build

      # The gap between what Reach selected and what the whole suite ran is the number worth
      # looking at, so keep the report whatever happened above.
      - name: Keep the report
        if: ${{ !cancelled() }}
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

`jq` is preinstalled on GitHub's hosted Ubuntu runners
([the `ubuntu-latest` image](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md)).
The `@sh` filter quotes each argv element for a POSIX shell
([jq manual](https://jqlang.org/manual/#format-strings-and-escaping)), which is why `eval` is safe
here and why Reach emits argv vectors rather than shell strings in the first place.

### Every other provider

One row each, rather than five copies of three steps. An unverified recipe is worse than a table
row, because it looks authoritative.

| Provider | Fetch the whole history | Reach detects it from |
|---|---|---|
| GitHub Actions | `fetch-depth: 0` on `actions/checkout` ([docs](https://github.com/actions/checkout#readme)) | `GITHUB_BASE_REF` |
| Azure DevOps | `fetchDepth: 0` on `checkout: self` ([docs](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/steps-checkout#shallow-fetch)) | `SYSTEM_PULLREQUEST_TARGETBRANCH` |
| GitLab CI | `GIT_DEPTH: 0` in `variables` ([docs](https://docs.gitlab.com/ci/runners/configure_runners/#shallow-cloning)) | `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` |
| Bitbucket Pipelines | `clone: depth: full` ([docs](https://support.atlassian.com/bitbucket-cloud/docs/git-clone-behavior/)) | `BITBUCKET_PR_DESTINATION_BRANCH` |
| Jenkins multibranch | turn off *Shallow clone* in the job's Git behaviours ([docs](https://plugins.jenkins.io/git/#plugin-content-advanced-clone-behaviours)) | `CHANGE_TARGET` |
| TeamCity | a Git *Checkout policy* other than *Shallow clone*; *Auto* can choose it ([docs](https://www.jetbrains.com/help/teamcity/git.html#git-checkout-policy)) | **nothing — pass `--base` explicitly** |

TeamCity is not detected on purpose: `teamcity.pullRequest.target.branch` is a build parameter
([pull requests](https://www.jetbrains.com/help/teamcity/pull-requests.html#Predefined+build+parameters+for+pull+requests)),
not an environment variable, and TeamCity passes only `env.` parameters to the build process as
environment variables
([parameter types](https://www.jetbrains.com/help/teamcity/configuring-build-parameters.html#Parameter+Types)).

**Reach never reads a *source* branch variable** — `GITHUB_HEAD_REF`,
`System.PullRequest.SourceBranch`, `Build.SourceBranch` and their equivalents. A source branch
resolves at or ahead of `HEAD`, which produces an empty change set and a green pipeline: the one
detection bug that under-selects rather than over-selects.

## The switches whose behaviour surprises

`select --help` describes every switch, and this section covers only the ones that surprise. The
rest do what they say: `-c|--configuration` and `-v|--verbosity` take the values `dotnet build`'s
do, and `--no-color` turns colour off in the summary.

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
