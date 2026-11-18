# 09: Integrate and verify the coding standard

**What to build:** With the batches merged, the integration branch is green under the coding standard: the build with warnings as errors and format verification pass on both CI legs, and the Windows tests pass. This is the point where the integration branch is promised green. See [the spec](../spec.md).

- Resolve any diagnostics left between batches, or any a batch's fix introduced. Those rules belong to the batch that owns them, under the same no-suppression rule.
- Gather every suppression that was added (the pre-approved CA5350/CA5351 plus any the maintainer approved) into a list under `## Comments`, quoting the approval for each.
- Record the upstream package issues for the maintainer under `## Comments`: `insert_final_newline = false` in the defaults; the automatic `InternalsVisibleTo` assumes `<Project>.Tests`; the test-project config disables GMNG01/GMDTR12, which do nothing here; the README sample shows version `0.0.2`. Add anything else the batches turned up.

The 7 known Linux test failures are expected to remain. They are tickets 10–12.

**Blocked by:** 05 (documentation rules), 06 (Meziantou and CA rules), 07 (Sonar and StyleCop rules), 08 (the fixture code meets the standard)

**Status:** ready-for-agent

- [ ] `dotnet build -c Release -warnaserror` passes on both CI legs, with zero diagnostics
- [ ] `dotnet format --verify-no-changes --no-restore` passes on both CI legs
- [ ] The Windows CI leg's tests pass
- [ ] The Linux CI leg fails only the 7 known tests
- [ ] The list of suppressions with approvals, and the list of upstream issues, are recorded under `## Comments`

## Comments

- Ticket 04 temporarily narrowed the Husky pre-commit hook (`.husky/task-runner.json`) to `dotnet format whitespace` and `dotnet format style` with `--verify-no-changes`, because the full check fails on every unfixable diagnostic while the build is red. This ticket must restore the single full `dotnet format --verify-no-changes --include ${staged}` task.
