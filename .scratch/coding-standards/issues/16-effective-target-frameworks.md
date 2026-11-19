# 16: Reach uses a project's effective target frameworks

**What to build:** Reach expects the assembly instances that MSBuild actually builds, including when a `TargetFramework` or `TargetFrameworks` property is inherited from `Directory.Build.props`. The repository's own root `Directory.Build.props` stops clobbering the Contoso fixture's multi-targeting, so the fixture builds `net8.0;net10.0` in place again. See [ticket 13's comments](13-both-ci-legs-green-on-the-pr.md#comments), product finding 4, and [the spec](../spec.md).

Maintainer decision: "Fix the bugs".

- The SDK multi-targets only while `TargetFramework` is empty. A non-empty `TargetFramework`, from the project or inherited, wins over any `TargetFrameworks`. A project's own empty `TargetFramework` clears an inherited one. Reach reads the same rule from the raw XML (no MSBuild evaluation, as before).
- The root `Directory.Build.props` no longer sets `TargetFramework`. Each Reach project declares its own.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] An inherited `TargetFramework` overrides a project's own `TargetFrameworks` in the expected assembly set (test-first)
- [ ] A project's own `TargetFramework` overrides inherited `TargetFrameworks` (test-first)
- [ ] A project's empty `TargetFramework` clears an inherited one, so the project multi-targets
- [ ] The Contoso fixture builds `net8.0` and `net10.0` in place as part of `Reach.slnx`
- [ ] `dotnet build Reach.slnx -c Release -warnaserror` and the acceptance tests pass
- [ ] Reach run on this repository after a normal build does not fail with "No assembly was found"

## Comments
