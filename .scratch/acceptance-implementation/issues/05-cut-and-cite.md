# Cut and cite: what the docs stop claiming

Status: resolved
Depends on: —
Spec: [§9.1](../../acceptance-layer/spec.md#91-the-rules), [§10.2](../../acceptance-layer/spec.md#102-cut), [§10.3](../../acceptance-layer/spec.md#103-help-and-the-parity-tests), [§11](../../acceptance-layer/spec.md#11-amendments-to-other-records)

## Goal

Every claim the docs make that nobody will prove comes out, and every claim about another tool
carries a source.

## Scope

**Cut from Reach and the docs:**

- **`calli-unresolved`**: remove it from `NoticeCodes`, `NoticeCatalogue` and the schema's notice
  index. Set the function-pointer register entry in `docs/limitations.md` to "Detectable: no", and
  say detection is follow-up work after the alpha.
- **`packageVersion`**: delete the schema row and `ReportEntry.PackageVersion`
  (`src/Reach.Core/Reporting/Report.cs`).
- **`--list-unselected`**: delete the option (`src/Reach.Cli/ReachCommandLine.cs`),
  `SelectRequest.ListUnselected`, and its line in `Cli/Golden/select-help.txt`. Amend the M1 spec
  (`.scratch/walking-skeleton/spec.md`) §4's option table, §13.2's table row and §13.5 to "counted,
  never enumerated", with a one-line note pointing to the acceptance-layer spec §10.2.

**Rewrite:**

- **README's `dotnet test --filter "$(…)"`**: replace with the real recipe from
  `docs/adopting-reach.md`, or a link to it. Do not add an output mode to Reach.
- **`--paths` help text**: change "the source path of every changed member" to match the schema
  (hop-by-hop paths, every hop a method). Update the help golden file.
- **`tests[].id`**: narrow "comparable across runs" in the schema to "stable for the same input".
- **Reserved `data` keys "mean the same thing everywhere"**: cut the claim, or narrow it to what an
  existing test checks. If a test does check it, say which in this ticket's comments and keep the
  claim.

**Cite or cut**, the claims about other tools (spec §9.2, "cited fact" rows):

- `dnx` with a bare id failing on a prerelease, with that message;
- `--ignore-exit-code 8` behaviour and the .NET 11 SDK change; `--zero-tests-policy strict`;
- `jq` preinstalled on hosted runners; `@sh` quoting argv;
- the fetch-history setting for Azure DevOps, GitLab, Bitbucket, Jenkins and TeamCity.

Each one gets an inline link to a primary source (vendor docs, SDK source or release notes). Where
no primary source can be found, cut the claim. The walking-skeleton decision tickets
(`.scratch/walking-skeleton/issues/21-…` and `22-…`) record where several of these were verified.
Start there.

## Acceptance criteria

- `calli-unresolved`, `packageVersion` and `--list-unselected` appear nowhere in `src/` or `docs/`
  (the register may say the notice does not exist).
- The help golden test passes with the new `--paths` text and without `--list-unselected`.
- Every remaining claim about another tool has a working source link next to it.
- `RegisterParityTests` and `WorkflowParityTests` pass. The README's recipe is the documented one,
  so check `WorkflowParityTests` does not need to scan the README too. If the README now carries a
  copy rather than a link, it does, and the test is extended.
- The M1 spec is amended as above.

## Out of scope

The notice-index and help-option parity tests (ticket 15). Emitting the two notices (ticket 04).

## Comments

**Done.** `calli-unresolved`, `packageVersion` (schema row and `ReportEntry.PackageVersion`) and
`--list-unselected` (option, `SelectRequest.ListUnselected`, help golden line, the
`Every_option_reaches_the_request` assertion) are gone from `src/` and `docs/`. The register's
function-pointer entry reads "Detectable: no". `--paths` help now says it adds hop-by-hop paths,
every hop a method; golden file updated. `tests[].id` narrowed to "the same input produces the same
`id`". The M1 spec's §4 table, §13.2 row and §13.5 are amended, with a note at §13.5.

**README.** The `dotnet test --filter "$(…)"` line is replaced by a link to the recipe in
`adopting-reach.md`, not a copy, so `WorkflowParityTests` does not need to scan the README (it
already scans it for the `dnx` pin).

**Reserved `data` keys: cut.** No test checks that `projects`, `assemblies`, `paths` and
`members` mean the same thing across codes, so the section now says only that keys are free-form
per code. The matching sentence in `Notice.cs`'s doc comment is cut too.

**Cited facts.** Each now has an inline primary source: `dotnet tool exec` docs for
`--prerelease`, a pinned `dotnet/sdk` permalink to the `IsNotFoundInNuGetFeeds` string; MTP docs
for `--ignore-exit-code` and `--zero-tests-policy`; the `dotnet test` (MTP) docs'
"Whole-run and per-module minimums" section and microsoft/testfx#7457 for the .NET 11 change;
the runner-images Ubuntu 24.04 readme for `jq`; the jq manual for `@sh`; vendor docs for each
provider's fetch-history setting; TeamCity's parameter-types and pull-request pages.

Findings, cut or corrected:

- **Cut**: "the zero-match module's line changes from *Zero tests ran* to *passed* and the
  annotation disappears". Observed in walking-skeleton research, but no primary source documents it.
- **Corrected**: TeamCity's "default checkout is deep". Its docs say the *Auto* checkout policy can
  choose a shallow clone, so the cell now says to use a policy other than *Shallow clone*.
- **Narrowed**: "TeamCity passes only `env.`-prefixed parameters to the build process". System
  parameters reach the build too, as properties, so it now says only `env.` parameters arrive as
  environment variables. Also, `teamcity.pullRequest.target.branch` is a predefined build
  parameter, not a configuration parameter.
- **Narrowed**: "`--ignore-exit-code 8` stopped working on .NET 11" now says what the docs say:
  .NET 11 decides the zero-tests verdict once for the whole run.
- **Left alone, out of this ticket's list**: two other claims about other tools in
  `adopting-reach.md` still have no link. They are "Git does not preserve timestamps" and
  MSBuild's incremental-build behaviour under `--no-build`. Ticket 16's audit should decide on
  them.
