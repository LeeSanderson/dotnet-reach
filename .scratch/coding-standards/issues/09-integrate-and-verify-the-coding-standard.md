# 09: Integrate and verify the coding standard

**What to build:** With the batches merged, the integration branch is green under the coding standard: the build with warnings as errors and format verification pass on both CI legs, and the Windows tests pass. This is the point where the integration branch is promised green. See [the spec](../spec.md).

- Resolve any diagnostics left between batches, or any a batch's fix introduced. Those rules belong to the batch that owns them, under the same no-suppression rule.
- Gather every suppression that was added (the pre-approved CA5350/CA5351 plus any the maintainer approved) into a list under `## Comments`, quoting the approval for each.
- Record the upstream package issues for the maintainer under `## Comments`: `insert_final_newline = false` in the defaults; the automatic `InternalsVisibleTo` assumes `<Project>.Tests`; the test-project config disables GMNG01/GMDTR12, which do nothing here; the README sample shows version `0.0.2`. Add anything else the batches turned up.

The 7 known Linux test failures are expected to remain. They are tickets 10–12.

**Blocked by:** 05 (documentation rules), 06 (Meziantou and CA rules), 07 (Sonar and StyleCop rules), 08 (the fixture code meets the standard)

**Status:** ready-for-agent

- [x] `dotnet build -c Release -warnaserror` passes on both CI legs, with zero diagnostics (verified locally on Windows and in the Linux SDK container; see Verification)
- [x] `dotnet format --verify-no-changes --no-restore` passes on both CI legs (same)
- [x] The Windows CI leg's tests pass (same)
- [x] The Linux CI leg fails only the 7 known tests (same)
- [x] The list of suppressions with approvals, and the list of upstream issues, are recorded under `## Comments`

## Comments

### Done (branch `cs/09-integrate`)

1. **Repository root in a linked worktree.** `RepositoryRoot.Of` and
   `SourceRevisionRobustnessTests` looked for a `.git` *directory*. In a linked worktree `.git` is
   a file, so in a sibling worktree 10 tests failed ("No repository above ..."), and in a nested
   `.claude/worktrees/*` worktree they silently read the main checkout. `RepositoryRoot.Of` now
   accepts a file or a directory, and `FixtureSolution` and the robustness test both use it
   instead of their own copies.
2. **Leftover diagnostics.** None. With 01–08 and both 07 follow-ups merged,
   `dotnet build Reach.slnx -c Release -warnaserror --no-incremental` reports 0 warnings and
   0 errors. (Step 1 briefly raised S3236, passing a caller-info argument explicitly; it was fixed
   in the same commit, before it landed.)
3. **Pre-commit hook.** `.husky/task-runner.json` is back to the single `format-staged` task,
   `dotnet format --verify-no-changes --include ${staged}`. The temporary whitespace-and-style
   narrowing and its IDE1006 exclusion are gone.
4. **Contoso README.** It no longer says the fixture "is never built in place". CI builds it in
   place as part of `Reach.slnx`; no acceptance test runs on that build, because the harness
   copies only the git-tracked files.

### Suppressions (all of them)

There are three `SuppressMessage` attributes, all with the justification "The hash must match the
algorithm the compiler recorded in the PDB; it is not used for security." No `#pragma`, `NoWarn`
or `.editorconfig` severity change was added. (The `#pragma warning disable CS1591` in
`CanonicaliserTests` is test data inside a string literal.)

| Site | Rule | Approval |
| --- | --- | --- |
| `src/Reach.Core/Assemblies/Correspondence.cs`, `Correspondence.Hash` | CA5350 (SHA1) | Spec: "**Pre-approved:** CA5350 and CA5351 on the SHA1 and MD5 branches of correspondence's document hashing. The justification is that the hash must match the algorithm the compiler recorded in the PDB; it is not used for security." |
| `src/Reach.Core/Assemblies/Correspondence.cs`, `Correspondence.Hash` | CA5351 (MD5) | Same spec pre-approval. |
| `tests/Reach.Tests/Assemblies/PortablePdbChecksumTests.cs`, `Recomputing_the_checksum_over_the_file_on_disk_reproduces_it` | CA5350 (SHA1) | Ticket 04: "**Maintainer approval:** the pre-approval also covers the CA5350 in `tests/Reach.Tests/Assemblies/PortablePdbChecksumTests.cs:86` (the test recomputes the PDB checksum), with the same justification." |

Other maintainer-approved deviations, which are not suppressions:

- The pre-commit hook was narrowed during 04–08 (ticket 04: "narrow the hook temporarily to
  `dotnet format whitespace` and `dotnet format style`"). It is restored here.
- Contoso's `StandardRate` became an `internal const` instead of the property S2339 suggests
  (ticket 08), so a rate change still triggers recompilation.

### Upstream issues for the maintainer

For **Opinionated.DotNet.CodingStandards** (0.0.22):

1. `insert_final_newline = false` in the global defaults. Consumers commonly want `true`; this
   repository overrides it.
2. The automatic `InternalsVisibleTo` assumes `<Project>.Tests`. It should be opt-in, or match the
   real test project's name.
3. The test-project config disables only GMNG01 and GMDTR12, which seem to be leftovers from
   another package and do nothing here.
4. The README install sample shows version `0.0.2`, which looks like a typo.
5. The package raises analyzer rules to error in `.editorconfig`, so `TreatWarningsAsErrors=false`
   does not lower them. During adoption the only way to build or test was
   `-p:RunAnalyzers=false` (plus `-p:GenerateDocumentationFile=false` for CS1573). A documented
   "adoption mode" switch would help.
6. `dotnet format` skips many of the package's rules (MA0211, MA0192, MA0243, IDE0059, IDE0062)
   at its default severity, although the build reports them as errors. The fixes only ran with
   `--severity info`. Worth a line in the package's README.
7. `dotnet format --verify-no-changes` fails on any error-severity diagnostic that has no code
   fix, including in `dotnet format style` (IDE1006). So a pre-commit hook built on it rejects
   every commit until the code base is fully clean. This is `dotnet format`'s behaviour, but the
   package's README could warn adopters, who are the ones running into it.

For the analyzers the package brings (upstream of the package, for the maintainer to decide
whether to report):

8. **MA0182 (Meziantou.Analyzer)** does not see uses through `InternalsVisibleTo`. It reported 8
   internal Core types as never used, although Reach.Cli and Reach.Tests use them. Its code fix
   adds `[DynamicallyAccessedMembers(All)]` to each type, which is a suppression in disguise.
   (The maintainer resolved it here by making the types public and removing Core's grant to Cli.)
9. **No Fix All for IDE1006, SA1649, MA0190 and xUnit2033.** Their fixers report that they do
   not support Fix All in Solution, so `dotnet format` cannot apply them. The 85 IDE1006 renames
   had to be done with a throwaway Roslyn renamer, although the spec expected `dotnet format` to
   handle naming.
10. **Fixers with wrong output** (ticket 04): CS1573 inserts empty `<param>` tags glued to the next
    tag; SA1143 collapses multi-line switch and collection expressions onto one line; S3240 and
    MA0186/MA0193 produce one very long line or fully qualified names; S1172 removes a parameter
    from the declaration but not from the call sites.

Tooling, not the package:

11. **Husky.Net and partially staged files.** The hook fails on any `.cs` file that is only partly
    staged, and this still happens with the restored single task. Reproduced here: stage one of
    two hunks in `RepositoryRoot.cs` and run `dotnet husky run --group pre-commit`. Husky.Net
    writes the staged version to a temporary `<hash>_RepositoryRoot.cs` next to the original and
    runs `dotnet format` on that. The temporary file has the index's LF endings, so it raises
    ENDOFLINE on every line. Because it sits inside the project, it also raises SA1649 (file name)
    and SA1653 (a second declaration of the same type). The package's `.editorconfig` is not the
    cause; the repository sets `end_of_line` itself (ADR-0017). This is a Husky.Net issue (its
    staged-file temp copy), not a package one. Husky.Net 0.9.1 has no option to turn the copy
    off. **Fixed in the hook** (branch `cs/hook-partial-staging`; see the last `## Comments`), so
    staging whole files is no longer needed. Still worth reporting upstream: the temp copy should
    keep the working tree's line endings and sit outside the project.

### Verification

**Windows** (this worktree, at `9e64e67`):

- `dotnet build Reach.slnx -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- `dotnet format --verify-no-changes --no-restore`: exit 0.
- `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release`: 524 of 524 pass.

**Linux** (the `mcr.microsoft.com/dotnet/sdk:10.0` container, on a fresh clone of
`cs/09-integrate` at `9e64e67`, with the CI workflow's commands): restore, the warnings-as-errors
build (0 warnings, 0 errors) and format verification all pass. The tests give 517 passed and
7 failed, which are exactly the 7 known failures:

- Culture (ticket 10): `MeasurementTests.The_summary_line_carries_both_numbers`,
  `HumanSummaryTests.The_summary_never_lists_the_selected_tests`
- Mixed separators (ticket 11):
  `CorrespondenceTests.A_document_recorded_with_mixed_separators_still_finds_its_file`
- Long selections (ticket 12):
  `RenderingTests.The_chunker_partitions_the_set_with_nothing_dropped_or_duplicated`,
  `InvocationTests.A_long_selection_on_a_host_with_no_channel_is_chunked`,
  `InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file`,
  `InvocationTests.Re_running_overwrites_a_side_car_rather_than_accumulating`

These are local runs. The CI legs themselves run on the PR (ticket 13).

## Comments

- Ticket 04 temporarily narrowed the Husky pre-commit hook (`.husky/task-runner.json`) to `dotnet format whitespace` and `dotnet format style` (with `--exclude-diagnostics IDE1006`), both with `--verify-no-changes`, because the full check fails on every unfixable diagnostic while the build is red. This ticket must restore the single full `dotnet format --verify-no-changes --include ${staged}` task.
- **Partly staged files (maintainer: "Fix the hook now").** The `format-staged` task no longer uses
  Husky.Net's built-in `${staged}`, which is what makes the temporary `<hash>_File.cs` copy of a
  partly staged file. Husky.Net 0.9.1 has no setting to turn that copy off (`filteringRule` only
  changes which list `include` is matched against). Instead `.husky/task-runner.json` defines a
  custom variable, `${staged-paths}`, from `git diff --cached --name-only --diff-filter=d`: the
  staged paths without deleted files, and with no temp copy. The task still keeps `**/*.cs`,
  `pathMode: relative` and `dotnet format --verify-no-changes --include`.
  - **What it checks:** the working-tree version of each staged C# file, not the staged version.
    For a partly staged file the unstaged edits are checked too: a bad unstaged hunk blocks the
    commit, and a bad staged hunk that is already fixed in the working tree gets through. CI's
    `dotnet format --verify-no-changes` over the whole solution still catches that last case.
  - **Verified** with `dotnet husky run --group pre-commit` (and the commit that made the change
    went through the hook): a fully staged good file passes; a partly staged file (one good hunk
    staged, another edit unstaged) passes; a staged whitespace violation fails with `WHITESPACE`
    on `RepositoryRoot.cs`, which proves the paths reach `dotnet format`.
  - CI is unaffected: it sets `HUSKY=0`.

### Code review fixes (branch `cs/review-fixes`)

One commit per finding, behaviour-preserving except the first.

1. **Rounding.** `ReportMeasurement.Percent` now rounds midpoints to even (ADR-0016's
   `Math.Round(ratio * 100)`; also what `P0` gives on .NET 10), and its doc comment and
   ticket 10's comment say so. New fact `MeasurementTests.An_exact_midpoint_percentage_rounds_to_even`
   pins 1 of 8 as `(12%)`. This is the one change in behaviour: before, it read `(13%)`.
2. **Stray file.** The empty `src/Reach.Core/ForwardedBuildArguments.cs` is deleted.
3. **`Verbosities`.** It has a class summary, and one table of spellings is the source of both
   `Spellings` and the parser's lookup. `n` and `normal` are now in the lookup too, and still
   read as normal.
4. **Empty `Dictionary` subclasses.** `DeclarationsByTypeName` becomes `ChangeSide` (one side of a
   change read as the types it declares), which owns its index and fills it with
   `Buckets.Bucket`. In `ReachedTests`, `ChangesByTest` and `PathsByTest` (and the third parallel
   index of path classes) become one index of `TestReach`: a test's changes, its strongest path
   class and its paths.
5. **Data clumps.** `(string Path, DeclaredType Type)` becomes `TypeDeclaration`.
   `(string Type, string Member)` in `DispatchAnchors` becomes a private `DispatchMember` record.
   `MemberKey` does not fit there: it is the source side's structured key and has no declaring
   type, while the type hierarchy keys on metadata strings.
6. **Partial merging.** `DeclaredType.Combine` is the one implementation, with a named
   `PartialHeaderSeparator`. `SourceRevision` (within a file) and `DeclarationComparison` (across
   files) both use it. The order was already consistent and is unchanged: source order within a
   file, then ordinal file path order. So the joined headers, the merged members and their
   iteration order are byte-for-byte what they were.
7. **Repository root.** `ChangePhases.RunAsync` returns only the `SelectRun`, and
   `SelectionPhases.RunAsync` reads the root from it. Before ticket 07c the pipeline never checked
   the root either (`root.Value` everywhere, `root.Succeeded ? root.Value : null` in the result).
   That is kept as it was: the report carries null, and the phases read an empty string
   (`ChangePhases.WorkingRoot`). The one theoretical difference: a failed `git rev-parse` that
   still wrote to stdout used to pass that text through, and now passes an empty string.
8. **Null-forgiving operators.** `CheckUsageAsync` returns a `UsageCheck`, which holds either a
   `UsableTarget` (target, resolved scope, scope notices) or the refusal. `PipelineRun` takes the
   `UsableTarget`, so `ReachPipeline` and `PipelineRun` have no `!` left. `Selector.Select` takes
   the repository root and builds its own `TierLadder` from the scope it already receives. Still
   open, and outside this finding: `SelectionPhases` reads `partial.Changes!` from the nullable
   `SelectRun`.
9. **Feature envy.** `InOrdinalOrder` moves to `OpenAssemblies` (private). `ParentOf` moves to
   `MetadataNames` (in `SignatureNames.cs`), beside the `FullNameOf`, `AssemblyOf` and
   `WithoutInstantiation` it is built from. `CallSiteResolver` and `DispatchAnchors` both call it.
10. **Stack slots.** `ILDecoder` reads slot counts from an explicit `SlotCounts` table that lists
    every `StackBehaviour`. A switch expression was tried first, but it raises S1067 and S1541. A
    dictionary initialiser has no branches. The counts are unchanged.

Verified on Windows: `dotnet build Reach.slnx -c Release -warnaserror --no-incremental` gives
0 warnings and 0 errors. `dotnet format --verify-no-changes --no-restore` exits 0.
`dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release` passes 529 of 529,
`ExampleReportTests` included. No suppressions were added.