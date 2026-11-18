# 05: Batch: documentation rules

**What to build:** Reach's own projects raise no documentation diagnostics under the coding standard: MA0211 (multi-line XML summaries), CS1573 (missing `<param>` on partially documented members and records), and any other XML-doc rules in ticket 04's list. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

Fix in code, one commit per rule. The documentation should still read well: reshape summaries; don't just pad them. **No suppression without the maintainer's explicit approval for that rule.** If a rule looks wrong for this codebase, stop and ask, and record the question under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [ ] Zero MA0211 and CS1573 diagnostics in Reach's own projects
- [ ] Zero remaining documentation-family diagnostics from ticket 04's list
- [ ] No suppressions added unless approved (approval quoted under `## Comments`)
- [ ] Reach's tests still pass with warnings not treated as errors
