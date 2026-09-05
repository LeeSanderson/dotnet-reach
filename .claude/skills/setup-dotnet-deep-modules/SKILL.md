---
name: setup-dotnet-deep-modules
description: Make each project in a .NET solution a deep module, with implementation hidden behind `internal` and reachable only through the project's public surface, and wire an architecture test suite that enforces it. User-invoked.
disable-model-invocation: true
---

# Setup .NET Deep Modules

Make every project in this solution a **deep module**: a lot of behaviour behind a small interface. A project's public surface is its **`public` types in its root namespace**, and everything under `Internal/` is hidden. Most of this is enforced by the C# compiler rather than a linter, which is the point: an `internal` type is not reachable from another assembly, full stop. This skill sets up that shape, then adds an architecture test project for the two rules the compiler cannot see, then proves the rules bite.

For the vocabulary (deep module, interface, seam, depth), call the Skill tool with "codebase-design" and use its language throughout.

## The shape this enforces

```
src/
  Reach.Analysis/
    Reach.Analysis.csproj
    ReachAnalyzer.cs        ← public, root namespace: part of the interface. Reference from outside.
    IAssemblyProbe.cs       ← another public type. Projects may expose SEVERAL.
    Internal/               ← implementation: `internal`, invisible outside the assembly.
      CallGraphBuilder.cs
  Reach.Cli/                ← the host: wires modules together, depends on all of them
tests/
  Reach.Analysis.Tests/     ← tests through the public surface only. No InternalsVisibleTo.
  Reach.ArchitectureTests/  ← the rules below, as tests
```

The public surface is the project's **`public` types in its root namespace**, not one designated facade type. By convention implementation lives in `Internal/`, which the SDK's folder-to-namespace convention turns into `<Project>.Internal` for free.

Four rules. Two are free, two are tests:

1. **Assembly boundary and a flat public surface** _(compiler + test)_. Code in another project can reach only `public` types, so implementation marked `internal` is unreachable by construction. The test guards the inverse, which the compiler has no opinion on: nothing under `Internal/` is `public`, and no `public` type hides in a deeper namespace. Both would be a leak the compiler happily allows.
2. **Intra-assembly freedom** _(compiler, free)_. A project's own files see each other, `internal` members included. Nothing to configure.
3. **Tests through the public surface** _(test)_. No project grants `InternalsVisibleTo`. Integration tests across modules are fine; reaching into internals is not.
4. **No cycles, and dependencies point inward** _(build + test)_. A circular `ProjectReference` is already a build error, so cycles are free. What's left is direction: no module may depend on the host. That's the NetArchTest rule, and it's where any other layering rules go.

**A small surface, not a facade.** Because the surface is *every* `public` root-namespace type, a project can expose several small types (`ReachAnalyzer`, `IAssemblyProbe`, `AnalysisResult`) instead of funnelling everything through one god-class. God-class facades are discouraged; keep the public types small and hide implementation under `Internal/`.

Layering beyond "nothing depends on the host" is a *different* concern, specific to this repo, and step 4 asks for it rather than guessing.

## Steps

### 1. Detect the environment

- **Solution and layout**: find the `*.sln` / `*.slnx`. Are projects under `src/` and `tests/`, or flat at the root? Use whatever the repo already does; confirm with the user if there's no obvious convention.
- **Module projects vs the host**: which project is the entry point (has a `Program.cs` / `OutputType` of `Exe`)? Everything else is a candidate module.
- **Existing architecture tests**: a `*.ArchitectureTests` / `*.Architecture.Tests` project, or a test class using `NetArchTest` / `ArchUnitNET`. If one exists, do **not** replace it: merge the rules in and tell the user what you added.
- **Existing `InternalsVisibleTo`**: search the `.csproj` files and any `AssemblyInfo.cs` for it. **Report every hit to the user before going further.** Each one is a test reaching past an interface, and rule 3 will fail on it. Agree with the user whether to remove them (moving those tests to the public surface) or to accept a documented exemption list; don't silently delete a grant that tests depend on.
- **Target framework and test stack**: the TFM in use, and whether tests are xUnit with AwesomeAssertions (the template below assumes both).

**Done when:** layout, host project, module candidates, existing-config status, and every existing `InternalsVisibleTo` grant are all known and the grants have been discussed.

### 2. Create the architecture test project

If there's no architecture test project, create one alongside the other test projects (`tests/<Solution>.ArchitectureTests`), add it to the solution, and reference **every module project** from it — it needs their assemblies to inspect. Then add the packages:

```bash
dotnet add tests/<Solution>.ArchitectureTests package NetArchTest.Rules
```

Plus xUnit and AwesomeAssertions if the project is new (match the versions the repo's other test projects use).

**Done when:** the architecture test project builds, is in the solution, and references every module project.

### 3. Write the architecture tests

Copy [`DeepModuleTests.cs`](./DeepModuleTests.cs) into the project. Adapt:

- the namespace, to match the project
- `Modules`: one marker type per module project (any `public` type in its root namespace)
- `HostAssemblyName`: the entry-point project's assembly name

NetArchTest's fluent surface shifts between versions. If the template doesn't compile, check the installed version's API and adapt: the rules are what matter, not the method names.

**Done when:** `dotnet test` runs the architecture tests and they pass on the current code.

### 4. Wire it into the checks and collect the real layering rules

- Architecture tests are just tests, so `dotnet test` already runs them. Confirm CI runs the whole test suite and not a filtered subset that excludes them.
- Ask the user for the layering rules this repo actually has — "the domain doesn't know about persistence", "nothing but the host touches the CLI parser" — and add one assertion per rule, following the shape of the host rule in the template. If they have none beyond the host rule, leave it at that and say so; a rule invented to fill space is worse than no rule.
- Do **not** add path aliases, restructure namespaces, or touch `Directory.Build.props` beyond what a new test project needs.

**Done when:** the architecture tests run in CI, and every layering rule the user named is an assertion.

### 5. Scaffold the example module

Create a committed `src/<Solution>.Example/` as a copy-me template:

- `Greeter.cs`: a `public` type in the root namespace that delegates to an internal type (so the module is visibly *deep*, not a pass-through).
- `Internal/GreetingFormatter.cs`: an `internal` type under `Internal/`, used by `Greeter`, unreachable from outside.
- `tests/<Solution>.Example.Tests/GreeterTests.cs`: references the project and asserts against `Greeter` only. No `InternalsVisibleTo`.

Add the example's marker type to `Modules` in the architecture tests. Tell the user this is a starter template to copy or delete.

**Done when:** the example module exists, exposes its behaviour through a public root-namespace type, hides the formatter under `Internal/`, and its test passes through the public surface.

### 6. Prove the rules bite

This is the completion criterion for the whole skill: a rule that doesn't fail on a violation is worthless. Prove both halves — the compiler and the tests.

1. Run `dotnet test`. It must **pass** on the clean example.
2. Change `Internal/GreetingFormatter.cs` from `internal` to `public`. Run again; `Implementation_types_are_not_public` must **fail**. Revert.
3. In the example's test project, try to use `GreetingFormatter` while it's `internal`. `dotnet build` must **fail** with CS0122 or CS0246 — that's the compiler doing the work no linter has to. Revert.
4. Add a `ProjectReference` from the example module to the host project. `dotnet build` must fail (a cycle) or `No_module_depends_on_the_host` must fail. Revert.
5. Run `dotnet test` once more; it must **pass**.

**Done when:** you have observed a pass, a failing architecture test, a failing compile, a failing direction rule, and a pass again. If any step doesn't fail, the rules aren't wired correctly, so fix before finishing.

### 7. Document the convention

Write a `README.md` **in the projects folder** (`src/README.md`, next to the projects it governs) covering: the layout (`public` types in the root namespace are the interface, `Internal/` is implementation and is `internal`, tests live in `tests/`), "reference a project only through its public types", and how to run the architecture tests. **Discourage god-class facades** explicitly: expose several small public types instead of one type that re-exports everything. State the `InternalsVisibleTo` ban and why. Keep it to the copy-me snippet plus the four rules in one paragraph each.

Then add a **context pointer** to it from the repo's agent-instructions file (`CLAUDE.md` if present, else `AGENTS.md`, creating `AGENTS.md` if neither exists). One line is enough, e.g. `Projects are deep modules: see [src/README.md](./src/README.md) before adding a project or a public type.` This is what makes an agent discover the boundary rule instead of tripping over it.

**Done when:** `src/README.md` exists and discourages facades, and the repo's `CLAUDE.md`/`AGENTS.md` links to it.

## Notes

- **The compiler is the enforcement mechanism, and that's the whole advantage.** A boundary rule that lives in a linter config can be skipped, disabled in CI, or forgotten by a new contributor. `internal` cannot: the reference doesn't compile. Only put in the architecture tests what the compiler genuinely cannot see.
- **`InternalsVisibleTo` dissolves the entire scheme,** which is why rule 3 bans it outright rather than allowing "just for tests". A test that needs internals is testing past the interface, and the module is probably the wrong shape. Test-only constructors and factory methods that are `public` and honestly part of the interface are the alternative.
- **One project per module is the price.** A `.csproj` is heavier than a folder: it costs build time and solution noise. Don't split a module into projects for tidiness; split when there's a boundary worth compiling. A module that will never have a second consumer can stay a folder inside an existing project, with `internal` still doing the work at the assembly edge.
- **Adding a `public` type to a root namespace is adding to the interface** and carries the same weight as adding an entry point. If the user wants that to be a deliberate, reviewable act, `Microsoft.CodeAnalysis.PublicApiAnalyzers` turns every addition into an explicit diff in `PublicAPI.Unshipped.txt`. Offer it; don't install it uninvited.
- **`Internal/` earns its name from the SDK's folder-to-namespace convention**, so the folder gives you `<Project>.Internal` with no configuration. Nest as deep as you like beneath it; the namespace test only cares that public types stay at the root.
- **Modules are flat**: one tier of projects under `src/`. A module's internals may nest as deep as you like; a project may not contain another project.
