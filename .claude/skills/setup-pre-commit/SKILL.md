---
name: setup-pre-commit
description: Set up Husky.Net pre-commit hooks with a task runner (dotnet format on staged files), build, and tests in the current repo. Use when user wants to add pre-commit hooks, set up Husky, configure staged-file formatting, or add commit-time formatting/build/test checks.
---

# Setup Pre-Commit Hooks

## What This Sets Up

- **[Husky.Net](https://alirezanet.github.io/Husky.Net/)** pre-commit hook, installed as a local dotnet tool
- **task-runner.json** running `dotnet format` against staged `.cs` files
- **.editorconfig** (if missing) — the formatting rules `dotnet format` obeys
- **build** and **test** tasks in the pre-commit hook

## Steps

### 1. Detect the environment

- **Solution**: find the `*.sln` / `*.slnx` at the repo root, or the single `*.csproj` if there's no solution. The tasks below run against it from the repo root.
- **Tool manifest**: `.config/dotnet-tools.json`. If it's missing, step 2 creates it.
- **Test project**: any `*.csproj` referencing `Microsoft.NET.Test.Sdk` (usually `*.Tests.csproj`). If there is none, omit the test task and tell the user.
- **Existing hooks**: a `.husky/` directory, or a `core.hooksPath` already set (`git config core.hooksPath`). If either exists, do **not** clobber it: merge the tasks in and tell the user what you added.

### 2. Install Husky.Net

```bash
dotnet new tool-manifest   # only if .config/dotnet-tools.json is missing
dotnet tool install Husky
```

The NuGet package is `Husky`; the CLI it provides is `dotnet husky`.

### 3. Initialize Husky

```bash
dotnet husky install
```

This creates `.husky/` (with `husky.sh` and a sample `task-runner.json`) and points `core.hooksPath` at it.

`core.hooksPath` is local git config, so it isn't cloned. Add this to `Directory.Build.props` at the repo root so a fresh clone installs the hooks on its first restore:

```xml
<Target Name="HuskyInstall" BeforeTargets="Restore;CollectPackageReferences"
        Condition="'$(HUSKY)' != '0'">
  <Exec Command="dotnet tool restore" StandardOutputImportance="Low" StandardErrorImportance="High" />
  <Exec Command="dotnet husky install" StandardOutputImportance="Low" StandardErrorImportance="High"
        WorkingDirectory="$(MSBuildThisFileDirectory)" />
</Target>
```

Set `HUSKY=0` in CI to skip it.

### 4. Create `.husky/pre-commit`

```bash
dotnet husky add pre-commit -c "dotnet husky run --group pre-commit"
```

The generated hook sources `husky.sh` and runs that command. Don't hand-write the hook file; let the CLI generate it so the shebang and `husky.sh` sourcing are right.

### 5. Write `task-runner.json`

Replace the sample Husky.Net generates with:

```json
{
  "tasks": [
    {
      "name": "format-staged",
      "group": "pre-commit",
      "pathMode": "absolute",
      "command": "dotnet",
      "args": ["format", "--verify-no-changes", "--include", "${staged}"],
      "include": ["**/*.cs"]
    },
    {
      "name": "build",
      "group": "pre-commit",
      "command": "dotnet",
      "args": ["build", "--nologo", "-warnaserror"]
    },
    {
      "name": "test",
      "group": "pre-commit",
      "command": "dotnet",
      "args": ["test", "--nologo", "--no-build"]
    }
  ]
}
```

`${staged}` expands to the staged files, and `include` narrows the task to `.cs` files, so the task is skipped entirely when a commit touches none.

**`--verify-no-changes` fails the commit instead of fixing it.** That's deliberate: `dotnet format` rewrites files in place, and Husky.Net does **not** re-stage what a task modified, so an auto-fixing hook commits a formatted version of code the developer never saw while leaving the fix unstaged. If the user would rather auto-fix, drop `--verify-no-changes` and add a follow-up task that re-stages (`git add ${staged}`), and tell them the commit will contain changes they haven't reviewed.

**Adapt**: omit the test task if there's no test project. If `dotnet build -warnaserror` fails on pre-existing warnings, drop `-warnaserror` rather than leaving a hook nobody can get past, and tell the user which warnings are in the way.

### 6. Create `.editorconfig` (if missing)

`dotnet format` has no opinions of its own; it applies `.editorconfig`. Only create it if none exists:

```ini
root = true

[*]
charset = utf-8
indent_style = space
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
csharp_style_namespace_declarations = file_scoped:suggestion
dotnet_sort_system_directives_first = true

[*.{csproj,props,targets,json,yml,yaml}]
indent_size = 2
```

### 7. Verify

- [ ] `.config/dotnet-tools.json` lists `husky`
- [ ] `.husky/pre-commit` exists and runs `dotnet husky run --group pre-commit`
- [ ] `task-runner.json` exists with the `pre-commit` group
- [ ] `git config core.hooksPath` returns `.husky`
- [ ] `.editorconfig` exists
- [ ] Run `dotnet husky run --group pre-commit` to verify it works

### 8. Commit

Stage all changed/created files and commit with message: `Add pre-commit hooks (Husky.Net + dotnet format)`

This will run through the new pre-commit hooks: a good smoke test that everything works.

## Notes

- `dotnet format` ships with the SDK from .NET 6 onwards; there's nothing extra to install.
- If the repo has no `end_of_line` in `.editorconfig`, `dotnet format` leaves line endings alone. If you add one, make it match `.gitattributes` or the first run rewrites every file.
- The format task is staged-only and fast; build and test are whole-solution and are not. If the hook gets too slow to live with, narrow the test task (`dotnet test --filter`) and let CI run the full suite — a hook people bypass with `--no-verify` is worse than a smaller hook they keep.
- `dotnet test --no-build` depends on the build task having run first, so keep the tasks in this order.
