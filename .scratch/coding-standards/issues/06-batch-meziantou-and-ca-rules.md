# 06: Batch: Meziantou and CA rules

**What to build:** Reach's own projects raise no Meziantou (MA) or .NET analyzer (CA) diagnostics under the coding standard. See [the spec](../spec.md), ADR-0016 and ADR-0003.

This is a batch on the integration branch. The build stays red until ticket 09.

- MA0181 / MA0182 (casts) and the other MA and CA rules in ticket 04's list are fixed in code, one commit per rule.
- **Culture rules (CA1305, MA0076 and similar):** name the **invariant** culture, never the current culture, per ADR-0016. The measurement summary line's exact output is ticket 10's job. If the analyzer flags it here, name the invariant culture and leave the percent rendering to ticket 10.
- **Pre-approved suppression:** CA5350 and CA5351 on the SHA1 and MD5 branches of correspondence's document hashing, each as a `SuppressMessage` attribute at that site, justified by: the hash must match the algorithm the compiler recorded in the PDB, and is not used for security. Do not drop SHA1/MD5 support.
- **Any other suppression needs the maintainer's explicit approval for that rule.** Stop and ask, and record the question under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [ ] Zero MA and CA diagnostics in Reach's own projects
- [ ] Every culture-sensitive formatting call fixed in this ticket names the invariant culture
- [ ] CA5350/CA5351 are suppressed only at the two checksum sites, with the agreed justification
- [ ] No other suppressions added unless approved (approval quoted under `## Comments`)
- [ ] Reach's tests still pass with warnings not treated as errors
