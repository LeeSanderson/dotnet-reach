# 04: Add the coding-standards package and apply the automatic fixes

**What to build:** Opinionated.DotNet.CodingStandards applies to every project, and everything `dotnet format` can fix automatically is fixed. What remains is listed by rule, for the batch tickets to work from. See [the spec](../spec.md).

This starts the **integration branch**. With warnings as errors, the build is expected to stay red until ticket 09. Tickets 05–08 are batches on the same branch.

- Add the package to the shared build props as a package reference with all assets private, **pinned to exactly 0.0.22**. Do not introduce Central Package Management.
- Run `dotnet format` (style and analyzers) over Reach's own projects and commit what it fixes automatically: naming such as `_`-prefixed fields, unused usings, whitespace. Leave fixture projects to ticket 08.
- Record the remaining diagnostics, grouped by rule ID with counts, under `## Comments` in this file.

**No suppressions in this or any batch ticket without the maintainer's explicit approval for that rule.** If a fix looks wrong rather than just laborious, stop and ask.

**Blocked by:** 01 (standardise on CRLF)

**Status:** ready-for-agent

- [ ] The package is referenced once, from the shared build props, at exactly 0.0.22, with all assets private
- [ ] Properties the repository already sets (e.g. `LangVersion`) still take effect
- [ ] The automatic fixes are committed, one commit per rule family where practical
- [ ] The remaining diagnostics, by rule ID and count, are recorded under `## Comments`
- [ ] Reach's tests still pass with warnings not treated as errors (behaviour is unchanged by the automatic fixes)
