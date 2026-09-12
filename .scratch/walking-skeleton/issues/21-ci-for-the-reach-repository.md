# CI for the Reach repository itself

Type: grilling
Status: resolved
Blocked by: (none — 10, 11, 13 resolved)

## Question

Graduated from the fog. This is the one ticket about Reach's *own* repository rather than
about the product: what runs on a push, and whether the tool is published anywhere during
M1. An implementation ticket cannot be written for it while the answer is unknown, and the
spec's acceptance criteria have nowhere to run without it.

**What does CI do on a push?** Build, test, and what else. [The fixture
catalogue](11-fixture-catalogue.md) settled that integration tests get their history from a
temp git repository per test, which makes them self-contained — but it also means CI needs
whatever `git` and SDK those tests assume.
[ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md) fixes the SDK floor at .NET
10, so the runner image and any `global.json` pin follow from it.

**Is the tool published during M1, and where?** NuGet, GitHub Packages, a GitHub release
artifact, or nowhere at all and consumers build from source. [Tool
packaging](03-tool-packaging-and-cli-library.md) decided the packaging and that install
guidance leads with `dnx` — which is install guidance for a *published* package, so the
two answers have to agree. If nothing is published in M1, that guidance is documentation
for a future state and [the documentation ticket](20-what-documentation-m1-ships.md) needs
to know.

**Does versioning need deciding now?** Only if something is published. If it is, the report
schema's compatibility promise (PRD v0.2) attaches to a version number, so the two are
linked.

**Does Reach run on Reach?** The obvious dogfooding move, and [project layout and
ports](10-project-layout-and-ports.md) leaned on it as an argument. Decide whether that is
an M1 CI step, a manual first-datapoint exercise —
[method identity](09-method-identity-and-performance-budget.md) deferred the performance
budget to "the first real run as the first datapoint" — or out of scope for now.

**What is the minimum that makes the implementation effort safe to hand off?** That is the
bar, not a complete pipeline. The destination is a spec plus implementation tickets for a
walking skeleton; CI earns its place here by unblocking those tickets, not by being
thorough.

Resolve at 80/20, per the map's Notes.

## Comments

**From [What documentation M1 ships](20-what-documentation-m1-ships.md),** which resolved
first and left two constraints here rather than deciding them:

1. **The GitHub Actions workflow is also a documentation artifact.** M1 ships one complete
   CI recipe, and it must be the same YAML this repository's own CI runs, asserted by a test
   comparing the fenced block in `docs/adopting-reach.md` against the workflow file. That
   ticket made adoption cost a *judged* criterion everywhere except here, because
   ADR-0010 makes exit 4 the likeliest first run anyone has and a wrong fetch-depth line in
   the docs breaks PRD §12's criterion on contact. So whatever CI does, it does it in a
   workflow file shaped to be readable as an example.
2. **Publishing is still open and the docs are waiting on it.** The README carries a
   two-line quickstart whose first line is a `dnx` install. If M1 publishes nothing, that
   line documents a future state and both the README and `docs/adopting-reach.md` need
   different wording — so this ticket's answer changes text that is already specified.

## Answer

**Three workflow files arriving in two phases, publication to NuGet.org, and a gate that never
trusts Reach's own selection.** Resolved under
[ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md). Supporting
fact-finds: [research/21-ci-and-publishing.md](../research/21-ci-and-publishing.md) and
[research/21-xunit-v3-mtp.md](../research/21-xunit-v3-mtp.md).

### Sequencing dissolves the bootstrap

The ticket inherited what looked like a circular constraint. [What documentation M1
ships](20-what-documentation-m1-ships.md) pinned the adoption recipe to *the same YAML Reach's
own CI runs* — but that recipe is a `pull_request` workflow installing a published package, and
this repository had neither. Making the two agree seemed to require either a workflow that
installs from `--add-source ./artifacts` while the docs show `dnx` (a parity test comparing two
things that differ on the line that matters most), or PR ceremony for a solo project with no
reviewer at the other end.

**The answer is phases, not a compromise.** Pre-publish, work keeps landing on `main` and CI
gates on push. At the first tag the package exists; PRs begin; the documented recipe becomes
runnable *with the published package*, so the workflow file and the fenced block in the docs are
byte-identical with nothing elided, and the parity test ships alongside the workflow rather than
before it. You cannot dogfood before you publish, and it turns out you do not need to.

This is a constraint on the **order** implementation tickets land in, which is
[write the spec](12-write-the-spec.md)'s to carry: `ci.yml` is buildable from the first
commit; `dogfood.yml`, the parity test, and the fenced block in `docs/adopting-reach.md` are one
unit of work that arrives with `0.1.0-alpha.1` and cannot be written earlier.

### A fact removed one of the choices: MTP is mandatory

The suite uses the latest xUnit — `xunit.v3` **4.0.0**, the same package version [filter
dialects](01-filter-dialects-and-runner-detection.md) found had changed the filter surface, which
puts Reach's own suite on the newest dialect Reach has to render for. That is the best dogfooding
available and was not an argument anyone had while charting.

It also settles what looked like a decision. On .NET 10, `dotnet test` against an `xunit.v3`
4.0.0 project with no `global.json` is a **hard build error** — *"Testing with VSTest target is no
longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later"* — and adding
`xunit.runner.visualstudio` does not rescue it, because the block comes from MTP v2's own MSBuild
targets. Getting VSTest back means swapping the package for `xunit.v3.mtp-off`. So
`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` is not a preference; the
file has to exist and has to say it.

This **corrects the first fact-find**, which concluded M1 needed no decision here because doing
nothing left VSTest. Doing nothing leaves a build error.

### The gate

`ci.yml`. Push to `main` now, gaining a `pull_request` trigger at first publish. Matrix
`ubuntu-latest` × `windows-latest`.

Restore → build `-warnaserror` → `dotnet format --verify-no-changes` →
`dotnet test -c Release --no-build --report-xunit-trx --results-directory ./TestResults`. TRX
needs no extra package via that switch; `--report-trx` and `--coverage` are Microsoft extensions
that exit 5 without their packages.

**Both operating systems, and this is the one place worth spending the minutes.** Reach identifies
a first-party assembly by the source documents its debug symbols point at, and routes a changed
file by matching those paths against the working tree — separators and case sensitivity are
exactly where Windows and Linux differ, and the tool is developed on Windows, so Linux is the
untested half. A path-comparison bug there is silent under-selection, the failure class the map's
Notes rank above everything. This is the correctness rule, not thoroughness.

**Two things that look like CI steps and are not.** The docs↔workflow parity test from [what
documentation M1 ships](20-what-documentation-m1-ships.md) and the
every-`blind-spot`-code-has-a-register-entry test from [the limitations
register](19-the-limitations-register.md) are both tests in the suite. `dotnet test` runs them and
CI needs no knowledge of either. The suite checks the documentation; CI only runs the suite.

**Three things the suite needs from the runner**, from [the fixture
catalogue](11-fixture-catalogue.md)'s temp-repository-per-test mechanism: `git` identity
configured, since `user.email` and `user.name` are unset on hosted runners and `git commit` fails
without them; a NuGet restore of the fixture solution's own packages; and
`<OutputType>Exe</OutputType>` on the test project, which MTP enforces with a bespoke MSBuild
error.

**No `actions/setup-dotnet`.** Both runner images already carry four .NET 10 feature bands
(10.0.111, 204, 303, 400). `global.json` pins the band the developer machine carries with an
explicit `rollForward: latestFeature`, so the pin is a floor rather than a cage — and the explicit
policy is the load-bearing half, because an omitted `rollForward` defaults to `patch`, the
narrowest there is, and every policy ends in "else fail".

**`dotnet format` runs in CI and as a pre-commit hook.** The hook keeps it out of the way; CI is
what makes it true.

### Publishing

NuGet.org, because the alternative breaks documentation that is already specified. [Tool
packaging](03-tool-packaging-and-cli-library.md) decided install guidance leads with
`dnx dotnet-reach@<version>`, and [what documentation M1 ships](20-what-documentation-m1-ships.md)
put that line in the README's two-line quickstart. `dnx` is a shim over `dotnet tool exec` and
resolves from the ambient `nuget.config` hierarchy with **no hardcoded nuget.org** — proven both
directions, a `<clear/>` breaks it and a local folder feed alone resolves it offline.

**GitHub Packages is therefore not an option**, and this is the finding that closed the question
rather than a preference: its NuGet feed requires authentication *"regardless of whether the
package is public or private"*, confirmed with live 401s. A quickstart whose first line only works
for the author is worse than no quickstart.

`release.yml`, triggered on a `v*` tag push: build, test, pack, push. **Trusted Publishing via
`NuGet/login@v1` with `permissions: id-token: write`** — not a stylistic choice either, since new
NuGet.org API keys have been capped at 30 days since 2026-08-17, which has closed the long-lived
secret route.

**The version is hand-edited** in `Directory.Build.props`, first value `0.1.0-alpha.1`. No MinVer
or Nerdbank.GitVersioning: version derivation is machinery for a release cadence that does not
exist yet. The constraint that decides it is that **a published package cannot be deleted, only
unlisted** — a mistaken `0.1.0-alpha.1` is permanent — so two deliberate human acts, editing the
version and pushing the tag, are the right amount of friction for an irreversible one. **No signing
certificate**: nuget.org repository-signs automatically, and registering a certificate makes
signing mandatory for the account from then on.

One consequence lands on already-written documentation. **`dnx dotnet-reach` with a bare package
id will not find a prerelease**, failing with a misleading "not found in NuGet feeds"; pinning
`@0.1.0-alpha.1` works, with no `--prerelease`. [Tool
packaging](03-tool-packaging-and-cli-library.md) already recommended the pinned form, so the
quickstart survives a prerelease M1 — but the pin is now a **requirement rather than a
preference**, and the docs must not drift to the bare form.

Separately, and useful later: `dotnet reach` dispatch depends only on `ToolCommandName`, not on the
package id — verified by publishing a package under one name and invoking it under another. The
package name and the verb are independent choices. All four candidate ids are free and `dotnet-` is
not a reserved prefix.

### Reach runs on Reach, and does not gate

`dogfood.yml`, from the first tag onward: `fetch-depth: 0`, `dnx dotnet-reach@<version>`, the
selection run **alongside** a full suite that keeps gating.

**Gating M1's own CI on M1's own selection would be the unproven narrowing PRD §8 forbids**, and
the instrument that would justify it is shadow mode, which is M2. Running both *is* a miniature
shadow mode: the full suite tells the truth, the selection says what Reach would have run, and the
gap between them is the first real datapoint — which is also what [method
identity](09-method-identity-and-performance-budget.md) deferred the performance budget to when it
declined to commit a wall-clock number. It stops being a second job the day M2 proves it can.

It is a separate file rather than a job inside `ci.yml` because the parity test compares a fenced
block against **a workflow file**; comparing it against one job of a larger file is a worse test of
a worse artifact.

### What is deliberately not done

Each for a stated reason, so that a later reader finds a decision rather than an omission:

- **No coverage gate.** A threshold nobody has agreed on is a step that always passes.
- **No `.snupkg` or SourceLink.** Reach reads *consumers'* debug symbols; its own are not
  load-bearing, which is a pleasing inversion but not an argument for shipping them.
- **No Dependabot.** M1's dependency surface is `System.CommandLine` and Roslyn, both pinned
  deliberately by [ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md).
- **No path filters on the gate.** A required check that never reports — because a docs-only
  commit filtered it out — blocks a PR permanently, which is a worse failure than a wasted build.
- **No branch protection requiring approvals.** PRs exist here to set `GITHUB_BASE_REF`, not to
  find a reviewer who does not exist.

### Surfaced, and escalated rather than absorbed

Under MTP, **a filter matching zero tests exits 8** — verified empirically, and it fires on "filter
matched nothing" as well as "project has no tests"; the new `--zero-tests-policy` default only
rescues the all-skipped case. Reach is a tool whose entire output is filters, so this is product
news, not CI news, and it points two ways at once. It is a hazard for a consumer who wires the
filter solution-wide rather than per-project, which [the report contract](07-the-report-contract.md)
already designed against by keying invocations on (test project, target framework) and emitting
**zero** invocations for an empty selection. It is also an **inversion**: a rendered filter that
fails to match a test the report says was selected used to be a silent under-selection, the worst
failure in the map, and under MTP it is a red build.

It contradicts [the fixture catalogue](11-fixture-catalogue.md)'s second mandatory assertion, which
asserts exit code 0 *"in every host"* — written before anyone knew a host exited 8 — and there are
now **three** xUnit v3 hosts with three different exit-code tables, the built executable not being
MTP unless `UseMicrosoftTestingPlatformRunner=true`. That is too much to bury in a CI answer, so it
graduates: [zero-match filters and the third runner host](22-zero-match-filters-and-the-third-runner-host.md).

The **documentation** half stays here. `docs/adopting-reach.md` owes the `--ignore-exit-code 8`
caveat, because its recipe is the one verified artifact and this is the second way a first run goes
wrong after [ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)'s
fetch depth.

### No ADR, no glossary change

The decision a future reader is most likely to query — *why does the test-selection tool not gate
its own CI on its own selection?* — is surprising without context and is a real trade-off, but it is
one line to reverse, so it fails the hard-to-reverse test. The reasoning lives here.

The glossary needed nothing, and that is a small vote of confidence in it: **dialect** was already
defined as the grammar *"a particular test framework, framework version and runner host accepts"*,
so a third host was a case the definition had anticipated rather than a term it lacked.

### Consequences for other tickets

- **[Write the spec](12-write-the-spec.md)** inherits the two-phase ordering above, and is now also
  blocked on ticket 22.
- **[What documentation M1 ships](20-what-documentation-m1-ships.md)**: its two open constraints are
  discharged — the parity target is `dogfood.yml`, and publishing happens, so the `dnx` quickstart is
  real rather than aspirational, provided it keeps the version pin.
- **[The fixture catalogue](11-fixture-catalogue.md)**: its "exit code 0 in every host" assertion is
  ticket 22's to restate.
