# Invocations pin the target framework where it is derivable

Every invocation Reach renders for a test project with **more than one assembly instance**
carries a framework selector — `dotnet test <project> -f <moniker> …`. The moniker is derived
from assembly metadata alone. Where metadata cannot supply it, Reach does **not** guess: it
emits one project-wide `run-all` invocation with no selector and reports
`framework-selector-underivable`.

Two facts drive this, and they pull in opposite directions.

## Without a selector, a correct selection fails the build

Under Microsoft.Testing.Platform a filter matching zero tests exits **8**. A multi-targeted
project runs every target framework under one `dotnet test`, so a filter naming a test that
exists under only one of them makes the others fail:

```
$ dotnet test --filter-class "MultiTfm.Net10OnlyTests"
  ...\bin\Debug\net8.0\MultiTfm.dll  (net8.0|x64)  Zero tests ran   Exit code: 8
  ...\bin\Debug\net10.0\MultiTfm.dll (net10.0|x64) passed
Test run summary: Failed!   error: 1   total: 1   succeeded: 1
EXIT=8
```

This is exactly the case the report's `(test project, target framework)` keying exists for —
[ADR-0006](0006-a-graph-node-is-an-il-method-definition.md) makes the target framework part of
method identity, so two frameworks of one project can legitimately select differently. Rendering
that correct answer without a selector turns it into a red build. `-f` and `--framework` both fix
it; passing a built `.dll` path does not, because `--test-modules` is a glob resolved under
`--root-directory` and rejects absolute paths.

The selector is only *needed* where a project contributes more than one assembly instance. A
single-targeted project has nothing to cross-contaminate, so it gets no selector and needs no
moniker — which is what makes the second fact survivable.

## Metadata does not carry the moniker

`-f` matches the project's **declared** target-framework string exactly and case-sensitively.
That string is not in the assembly. Every one of these stamps the identical
`TargetFrameworkAttribute`:

```
net10.0                      → TargetFrameworkAttribute(".NETCoreApp,Version=v10.0")
net10.0-windows              → TargetFrameworkAttribute(".NETCoreApp,Version=v10.0")
                               TargetPlatformAttribute("Windows7.0")
net10.0-windows10.0.19041.0  → TargetFrameworkAttribute(".NETCoreApp,Version=v10.0")
                               TargetPlatformAttribute("Windows10.0.19041.0")
```

`FrameworkDisplayName` reads `.NET 10.0` for all three and carries no suffix.
`TargetPlatformAttribute` recovers the platform but **always with a version**, so
`net10.0-windows` reads back as `Windows7.0` and reconstructs to `net10.0-windows7.0` — which
`dotnet test -f` rejects. Worse, the mapping is not injective: a project declaring
`net10.0-windows7.0` produces byte-identical attributes to one declaring `net10.0-windows`, so
**no reconstruction rule can be correct for both**. `deps.json` and `runtimeconfig.json` do not
carry it either; the latter reports `"tfm": "net10.0"` even for the windows build.

The failure mode of a wrong moniker is hostile. Under an MTP runner it produces:

```
global.json defines test runner to be Microsoft.Testing.Platform.
All projects must use that test runner.
EXIT=1
```

— not a word about frameworks.

So the rule Reach adopts is the one metadata can support:

- `.NETCoreApp,Version=vN.M` → `netN.M`; `.NETStandard,Version=v2.0` → `netstandard2.0`.
- **No `TargetPlatformAttribute` → no suffix, and the moniker is exact.**
- `TargetPlatformAttribute` present → the declared moniker is unrecoverable. Stop.

That covers every multi-targeted test project that does not carry a platform suffix, which is
effectively all of them.

## Considered options

**The output directory name.** It *is* the exact declared moniker under the default layout,
verified across five probe targets — `bin\Debug\net10.0-windows\` is the literal string `-f`
wants. Rejected because it reintroduces precisely the path-trust that
[ticket 14](../../.scratch/walking-skeleton/issues/14-assembly-discovery-under-ambiguous-output-layouts.md)
spent its whole resolution removing, and it breaks under custom `OutputPath`,
`AppendTargetFrameworkToOutputPath=false` and the artifacts layout — the same ambiguous layouts
that motivated reading metadata in the first place. It buys precision in the one place the path
is least trustworthy.

**`dotnet msbuild <project> -getProperty:TargetFrameworks`.** The only *exact* answer. It returns
the verbatim declared strings including the `net10.0-windows7.0` form, and it evaluates without
building, so it is compatible with `--no-build`. Rejected for M1 because it puts MSBuild back
into a discovery story built to work without it, and pays one process launch per project on every
run to serve a case most solutions do not have. **Recorded as the upgrade path**, and it is a
contained one: a third adapter behind the `IProcessRunner` port that already carries `git` and
`dotnet build`.

**Emit the reconstructed moniker and accept the risk.** A wrong `-f` exits 1, which under Reach's
invariant means "run the whole suite", so it is not *unsafe*. Rejected because it ships a command
known to be broken, with an error message that names the wrong subsystem.

## Consequences

**Identity is unaffected, but the TFM is now read from two attributes.** `TargetPlatformAttribute`
separates `net10.0` from `net10.0-windows`, so discovery still sees two distinct assembly
instances and ticket 14's "two candidates for one instance" error does not misfire. What is lost
is only the declared *string*, and only the command line needs it.

**A new entry shape.** In the degenerate case the project's entries are all `mode: run-all`, and
the single project-wide invocation is carried by the **first entry in the report's own sort
order**; the rest carry zero invocations. A consumer's loop therefore issues exactly one command
and runs everything, which is correct and non-redundant. This makes `run-all` with zero
invocations representable for the first time — previously zero invocations implied `skip` — so a
consumer reading one entry in isolation must not infer "nothing to run" from an empty array
alone. The `framework-selector-underivable` notice locates every affected entry.

**The cost is over-selection, and it is named.** The project runs in full across every target
framework. Registered as an over-selection entry with the MSBuild call as its upgrade path, per
[ADR-0012](0012-m1-resolves-open-design-questions-at-80-20.md).

**Revisit trigger:** a platform-suffixed test project appearing in a real adopter's solution
alongside a plain one. That is the only shape this degrades, and one real instance is worth more
than the argument above about how rare it is.
