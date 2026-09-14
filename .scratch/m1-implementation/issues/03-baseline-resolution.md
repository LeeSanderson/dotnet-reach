# Baseline resolution

Status: ready-for-agent
Depends on: 02
Spec: [§7](../../walking-skeleton/spec.md#7-the-baseline) · [ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)

## Goal

Turn "no arguments in a CI job" into a **baseline** SHA, or into exit 4 with a message that
fixes the problem in one line.

## Scope

**One option, `--base <ref>`, always merge-base semantics**: the baseline is
`merge-base(HEAD, <ref>)`. Do not add a second `--baseline` option for an exact commit — it
collapses, because `merge-base(HEAD, C) == C` for any ancestor commit, and the inputs where
the two genuinely differ are diverged commits, where merge-base is the answer the caller
wanted anyway. Two options differing by three characters is a defect in a tool whose most
dangerous failure is a wrong baseline.

**The detection order** when `--base` is absent. First hit wins; every resolution is disclosed
in the human summary and as an `environment` notice carrying the resolved SHA.

| # | Variable | Provider | Note |
|---|---|---|---|
| 1 | `GITHUB_BASE_REF` | GitHub Actions | bare branch name |
| 2 | `SYSTEM_PULLREQUEST_TARGETBRANCH` | Azure DevOps | **format varies by repo provider** — `refs/heads/main` for Azure Repos, bare `main` for GitHub-hosted; strip the prefix |
| 3 | `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` | GitLab CI | |
| 4 | `BITBUCKET_PR_DESTINATION_BRANCH` | Bitbucket Pipelines | |
| 5 | `CHANGE_TARGET` | Jenkins multibranch | |
| 6 | exactly one of `origin/main` / `origin/master` present locally | — | local convenience; never fires in CI |
| 7 | — | — | **exit 4** |

**TeamCity is deliberately absent** — `teamcity.pullRequest.target.branch` is a configuration
parameter and TeamCity passes only `env.`-prefixed parameters to the build process. Do not add
it; its documentation shows `--base` explicitly.

**There is no default-branch fallback, and adding one is the first thing a reader will
propose.** `refs/remotes/origin/HEAD` does not exist after a CI checkout at any fetch depth,
because neither GitHub Actions nor the Azure Pipelines agent runs `git clone` — both do
`git init`, `git remote add`, `git fetch`, and only `clone` creates that ref. Recovering it
means a network call inside a step whose whole purpose is to be cheap and offline. Rung 6
probes for concrete refs for exactly this reason.

**Exit 4's message is a product surface, not an error string.** `actions/checkout` defaults to
`fetch-depth: 1` and on a PR build fetches one refspec, so `merge-base` has neither the ref nor
the history — **Reach does not work under default CI settings**, which makes exit 4 the
likeliest result of anyone's first pipeline run. The message detects the provider from the
environment and prints the **literal line to add**: `fetch-depth: 0` for GitHub Actions,
`fetchDepth: 0` for Azure DevOps, and the equivalent for the others. "Baseline unresolvable"
alone converts a one-line fix into a support question.

A **shallow clone** is exit 4 with the same treatment, never a silently truncated history.

**A baseline resolving to `HEAD` emits its own notice**, whether or not the change set turns
out empty. The resolved SHA leads the human summary and sits in the report envelope: the
empty-change-set trap *is* a baseline that quietly resolved to `HEAD`, and an audit trail that
cannot say what it audited will not catch it.

## Acceptance criteria

- A test per rung, driving the environment through the fake and asserting the chosen variable
  and the resolved SHA.
- **A named negative test per source-branch trap**, asserting each is never read:
  `GITHUB_HEAD_REF`, `System.PullRequest.SourceBranch`, `CI_MERGE_REQUEST_SOURCE_BRANCH_NAME`,
  `BITBUCKET_BRANCH`, `CHANGE_BRANCH`, and Azure's `Build.SourceBranch` — which is set on
  *every* build and reads `refs/pull/1/merge` on a PR. This is the one detection bug that
  under-selects rather than over-selects: a source branch resolves at or ahead of `HEAD`,
  giving an empty change set and a green pipeline.
- Azure's two formats both strip to the same branch name.
- An integration test over a temp repository with a **shallow clone** exits 4.
- Exit 4's message contains the literal `fetch-depth: 0` under a simulated GitHub Actions
  environment and `fetchDepth: 0` under Azure DevOps.
- A merge-commit checkout (detached `HEAD` whose parent is the target commit) resolves to
  exactly the target commit, including when the target branch has advanced since.
- A baseline resolving to `HEAD` emits its notice.

## Out of scope

Everything downstream of the SHA. This ticket produces a baseline and a detection record; the
change set is ticket 06.
