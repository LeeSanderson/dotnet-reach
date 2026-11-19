# 16: Reach uses a project's effective target frameworks

**What to build:** Reach expects the assembly instances that MSBuild actually builds, including when a `TargetFramework` or `TargetFrameworks` property is inherited from `Directory.Build.props`. The repository's own root `Directory.Build.props` stops clobbering the Contoso fixture's multi-targeting, so the fixture builds `net8.0;net10.0` in place again. See [ticket 13's comments](13-both-ci-legs-green-on-the-pr.md#comments), product finding 4, and [the spec](../spec.md).

Maintainer decision: "Fix the bugs".

- The SDK multi-targets only while `TargetFramework` is empty. A non-empty `TargetFramework`, from the project or inherited, wins over any `TargetFrameworks`. A project's own empty `TargetFramework` clears an inherited one. Reach reads the same rule from the raw XML (no MSBuild evaluation, as before).
- The root `Directory.Build.props` no longer sets `TargetFramework`. Each Reach project declares its own.

**Blocked by:** None

**Status:** ready-for-agent

- [x] An inherited `TargetFramework` overrides a project's own `TargetFrameworks` in the expected assembly set (test-first)
- [x] A project's own `TargetFramework` overrides inherited `TargetFrameworks` (test-first)
- [x] A project's empty `TargetFramework` clears an inherited one, so the project multi-targets
- [x] The Contoso fixture builds `net8.0` and `net10.0` in place as part of `Reach.slnx`
- [x] `dotnet build Reach.slnx -c Release -warnaserror` and the acceptance tests pass
- [x] Reach run on this repository after a normal build does not fail with "No assembly was found"

## Comments

Reach already read the nearest `Directory.Build.props`, so no ADR conflict: the raw-XML design stands. The bug was precedence. Reach preferred `TargetFrameworks` over `TargetFramework`, but the SDK multi-targets only while `TargetFramework` is empty (`IsCrossTargetingBuild`). Reach now follows that rule. No ADR: the rule is MSBuild's own and is documented at `ProjectFileReader.TargetFrameworksOf`. No limitation is added or removed. Conditions are still read literally, as before.

The root props could not keep a conditional `TargetFramework` (for example `Condition="'$(TargetFrameworks)' == ''"`): `Directory.Build.props` is imported before the project body, so the condition is always true there. Instead, `Reach.Cli`, `Reach.Core` and `Reach.Tests` each declare `net10.0`. The Contoso projects already declared their own.

Checked on Windows: `dotnet build Reach.slnx -c Release -warnaserror` is clean, and Contoso.Core builds `bin/Release/net8.0` and `bin/Release/net10.0` in place. The Release suite passes, 532/532. The net8.0 targeting pack comes from NuGet with the .NET 10 SDK. The acceptance tests already built the fixture's net8.0 in the `sdk:10.0` container and on both runners (ticket 13), so CI needs no extra setup.

Dogfood with base `HEAD~3`. With `--artifacts-path` inside the tree and `--no-build`, the exit code is 0. All 449 tests are selected, because `Directory.Build.props` changed, which correctly widens to everything. There is no "No assembly was found" failure. With the default layout (`-c Release --no-build`), the run still fails with exit 3. This is a separate cause: "More than one build of Reach.Core (net10.0)". The copies in `src/Reach.Cli/bin` and `tests/Reach.Tests/bin` are ambiguous by design (walking-skeleton ticket 14). `dogfood.yml` runs exactly that command, so it will hit this too. That belongs to ticket 15 (dogfood) or a maintainer decision, not to this ticket.
