# 07: Batch: Sonar and StyleCop rules

**What to build:** Reach's own projects raise no SonarAnalyzer (S) or StyleCop (SA) diagnostics under the coding standard. That also covers any remaining long-tail rule from ticket 04's list that tickets 05 and 06 don't own. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

- S109 (magic numbers): name the constant. Many of Reach's numbers are documented limits (e.g. the command-line ceilings in ADR-0009), and a named constant should point at their reasoning.
- S1541 / S3776 / S1067 (complexity): refactor without changing behaviour. Where a split would hurt readability, stop and ask rather than suppress.
- SA1649 (file name must match the first type), plus the other S and SA rules in the list.
- Fix in code, one commit per rule. **No suppression without the maintainer's explicit approval for that rule.** Record any request under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [ ] Zero S and SA diagnostics in Reach's own projects
- [ ] No diagnostics left from ticket 04's list that no batch owns
- [ ] No suppressions added unless approved (approval quoted under `## Comments`)
- [ ] Reach's tests still pass with warnings not treated as errors
