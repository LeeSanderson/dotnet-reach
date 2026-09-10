# The baseline is auto-detected from CI, with no default-branch fallback

Reach takes one baseline option, `--base <ref>`, and always applies merge-base semantics:
the baseline is `merge-base(HEAD, <ref>)`. When it is not given, the ref is detected from
the environment in a fixed order, and every resolution is disclosed — in the human summary
and as an `environment` notice carrying the resolved SHA.

There is **no fallback to the remote's default branch**, because in CI there is no such
thing to fall back to. That absence is the reason this decision is written down: it is the
first thing a future reader will propose adding.

## One option, not two

The obvious surface is two options — `--base <branch>` meaning merge-base and
`--baseline <commit>` meaning "this exact commit". The second collapses into the first. For
any commit that is an ancestor of `HEAD`, `merge-base(HEAD, C) == C`, so merge-base
semantics already deliver exact-commit behaviour for every commit anyone would realistically
name. The inputs where the two genuinely differ are commits that have *diverged* from `HEAD`
— and there, merge-base is the answer the caller wanted anyway, while an exact diff would
attribute another branch's work to this change.

One option also removes a support burden: `--base` and `--baseline` differ by three
characters and mean different things, which is a defect in a tool whose most dangerous
failure is a wrong baseline.

## The detection order

First hit wins. Each is disclosed as an `environment` notice.

| Order | Variable | Provider | Note |
|---|---|---|---|
| 1 | `GITHUB_BASE_REF` | GitHub Actions | Bare branch name. Set only for `pull_request` / `pull_request_target` |
| 2 | `SYSTEM_PULLREQUEST_TARGETBRANCH` | Azure DevOps | **Format varies by repo provider** — `refs/heads/main` for Azure Repos, bare `main` for GitHub-hosted. Strip the prefix |
| 3 | `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` | GitLab CI | Merge-request pipelines only |
| 4 | `BITBUCKET_PR_DESTINATION_BRANCH` | Bitbucket Pipelines | PR-triggered builds only |
| 5 | `CHANGE_TARGET` | Jenkins multibranch | Unset on ordinary branch jobs |
| 6 | exactly one of `origin/main` / `origin/master` present locally | — | Local-developer convenience only |
| 7 | — | — | Exit 4, baseline unresolvable |

**TeamCity is deliberately absent.** Its `teamcity.pullRequest.target.branch` is a
configuration parameter, and TeamCity does not pass parameters of that type to the build
process — only `env.`-prefixed ones reach it. So it cannot be detected, and TeamCity's
documentation must show `--base` explicitly.

Each provider also exposes a *source* branch variable that is easy to reach for by mistake,
and picking one is the one detection bug that under-selects rather than over-selects: a
source branch resolves at or ahead of `HEAD`, producing an empty change set and a green
pipeline. `GITHUB_HEAD_REF`, `System.PullRequest.SourceBranch`,
`CI_MERGE_REQUEST_SOURCE_BRANCH_NAME`, `BITBUCKET_BRANCH` and `CHANGE_BRANCH` are the traps.
So is Azure's `Build.SourceBranch`, which is set on *every* build and reads
`refs/pull/1/merge` on a PR.

## Why auto-detection is safe, and why the fallback rung is thin

Requiring `--base` always would be the cautious-looking choice, but the failure modes here
are asymmetric. Resolving to a branch that is too *old* yields a larger change set — over-
selection, which is safe. Only resolving at or ahead of `HEAD` under-selects, and that comes
from picking the source branch, which is a bug to test for rather than a reason to tax every
adopter with a required argument PRD §12's adoption criterion is measured against.

Rung 6 inherits that asymmetry: guessing `main` in a repository whose real base is `develop`
produces an older merge-base, so it over-selects. It exists for the local developer, where
no CI variable is set, and never fires in CI.

## Why there is no default-branch fallback

`refs/remotes/origin/HEAD` — the ref that records the remote's default branch — **does not
exist after a CI checkout, at any fetch depth**. Neither `actions/checkout` nor the Azure
Pipelines agent runs `git clone`; both do `git init`, `git remote add`, `git fetch`. Only
`git clone` creates `origin/HEAD`; `git remote add` writes fetch config but creates no refs,
and `git fetch` never creates it.

Recovering it means a network call — `git remote set-head origin -a` or
`git ls-remote --symref` — which is a network dependency inside a step whose whole purpose is
to be cheap and offline. Hence rung 6 probes for concrete refs rather than asking git what the
default branch is.

## Exit 4 is the likeliest outcome of a first run, so its message is a product surface

`actions/checkout` defaults to `fetch-depth: 1` and, on a PR build, fetches exactly one
refspec: `+<sha>:refs/remotes/pull/<n>/merge`. There is no `origin/<target>` ref and no
history behind `HEAD`, so `git merge-base` fails with a bad-revision error. Azure DevOps is
less predictable still: since sprint 209 its shallow-fetch default is conditional on the
organisation and the pipeline's age, so neither state can be assumed.

The consequence is that **Reach does not work under default CI settings**, and the fix is one
line of YAML. That makes exit 4 the most likely result of anyone's first pipeline run, and its
message a designed artifact rather than an error string: it detects the provider from the
environment and prints the literal line to add — `fetch-depth: 0` for GitHub Actions,
`fetchDepth: 0` for Azure DevOps. "Baseline unresolvable" alone would convert a one-line fix
into a support question.

## The merge commit is a help, not a hazard

Both GitHub Actions and Azure DevOps build a *merge commit* on PR validation rather than the
PR head, and check out a detached `HEAD`. This does not disturb the arithmetic. The target
commit is a parent of that merge commit and therefore an ancestor of `HEAD`, so `merge-base`
returns it exactly, and the diff is precisely the PR's changes — including when the target
branch has advanced since the merge ref was computed, since the older target commit remains
the best common ancestor. The only obstacle in these environments is the fetch depth above.

## Consequences

Reach's documentation owes a CI recipe per provider whose first line is the fetch-depth
setting, and a TeamCity recipe that passes `--base` explicitly.

The resolved baseline SHA leads the human summary on every run and sits in the report
envelope. This is not decoration: ticket 04's empty-change-set trap *is* a baseline that
quietly resolved to `HEAD`, and an audit trail that cannot say what it audited will not catch
it. A baseline resolving to `HEAD` emits its own notice whether or not the change set turns
out empty.
