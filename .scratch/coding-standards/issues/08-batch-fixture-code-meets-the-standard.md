# 08: Batch: the fixture code meets the standard

**What to build:** Built in place as part of the main solution, the fixture solution raises no diagnostics under the coding standard, and the acceptance tests still find and edit the lines they expect. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

- Built in place, the fixture projects inherit the root build props, and with them the package. Fix their code, including `dotnet format`'s automatic fixes.
- The acceptance tests edit fixture files by **exact text replacement**. Wherever a fix rewrites a line one of those edits matches (e.g. S109 on the `100` in the `Tax` expression), change the acceptance test in the **same commit**.
- The fixture must still model what it models: a multi-targeted core project, a source generator, an xUnit v3 project, an NUnit project, and a deliberately framework-less test project. A fix that would remove one of those shapes needs the maintainer's approval instead.
- **No suppression without the maintainer's explicit approval for that rule.** Record any request under `## Comments`.

**Blocked by:** 03 (the fixtures join the main solution), 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [ ] Zero diagnostics in the fixture projects when built in place
- [ ] `dotnet format --verify-no-changes` reports nothing for the fixture projects
- [ ] Every exact-text edit in the acceptance tests still matches its fixture line
- [ ] The acceptance tests pass (the temporary-repository build stays plain)
- [ ] No suppressions added unless approved (approval quoted under `## Comments`)
