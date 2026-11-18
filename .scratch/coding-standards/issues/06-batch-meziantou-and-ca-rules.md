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
- [x] Every culture-sensitive formatting call fixed in this ticket names the invariant culture
- [x] CA5350/CA5351 are suppressed only at the two checksum sites, with the agreed justification
- [x] No other suppressions added unless approved (approval quoted under `## Comments`)
- [x] Reach's tests still pass with warnings not treated as errors

## Comments

### Done (branch `cs/06-ma-ca`, one commit per rule)

- **MA0181 (casts, 50):** fixed without changing behaviour. Integer narrowing uses
  `int/uint/ushort/sbyte.CreateTruncating`, which has the same unchecked semantics as the cast.
  `ILOpCode` to and from its encoded value goes through a small `Graph/OpCodeValues` helper
  using `Unsafe.BitCast`, because this is the per-instruction IL decode path and `Convert`
  would box. Enum exit codes become ints through `Convert.ToInt32(code, CultureInfo.InvariantCulture)`.
  The rest use typed lambdas (`MetadataReference (path) => ...`), spreads, plain enum
  comparison, `Ticks / TimeSpan.TicksPerMillisecond` (the same truncation as `(long)TotalMilliseconds`),
  and `Split(Array.Empty<char>(), ...)` (documented as identical to a null separator).
- **MA0193 + RS0030:** `Math.Round(..., 4, MidpointRounding.ToEven)`, which is what the
  two-argument overload already did. Ticket 10 still owns the summary line's rendering.
- **MA0186:** `[NotNullWhen(true)]` on `MemberKey.Equals`.
- **MA0190:** the five `GeneratedRegex` methods are partial properties.
- **MA0191:** `CallGraphBuilder`'s type hierarchy and the `Graphs` and `Selections` fixtures'
  results were `= null!`. They are now field-backed properties (C# 14 `field`) whose getters
  throw if they are read before they are set.
- **CA1849:** async file I/O in the five flagged async tests (also clears S6966 there).
- **CA1869:** the two test `JsonSerializerOptions` are cached in static fields.
- **CA1001:** `CommandLineTests` is sealed and disposes its two `StringWriter`s.
- **CA5350/CA5351:** `SuppressMessage` on `Correspondence.Hash` (CA5350 and CA5351) and on
  `PortablePdbChecksumTests.Recomputing_the_checksum_over_the_file_on_disk_reproduces_it`
  (CA5350), each with the justification "The hash must match the algorithm the compiler
  recorded in the PDB; it is not used for security." These are the pre-approved sites recorded
  in ticket 04. No other suppressions were added.
- **MA0225:** `RespectRequiredConstructorParameters = true` on all three flagged options. Each
  of them is only used to *serialise*, and this setting only affects deserialisation, so
  nothing changes.
- **Culture:** no CA1305 or MA0076 was raised in Reach's own projects. The one formatting
  call this ticket added, the exit-code conversion, names the invariant culture.

Tests: `dotnet test --project tests/Reach.Tests -p:TreatWarningsAsErrors=false -p:RunAnalyzers=false
-p:GenerateDocumentationFile=false` gives 520 passed and 1 failed. The failure is the known
`AcceptanceTests.Fact_added_to_an_existing_method_selects_it` (ticket 08). The extra
`GenerateDocumentationFile=false` is needed now because the CS1573s are errors under the
package's `.editorconfig`, and `TreatWarningsAsErrors=false` does not lower them.

### Open: questions for the maintainer (rules stopped, not fixed)

**MA0182, 8 types (`ColourSupport`, `ProcessRunner`, `ReachPipeline`, `HumanSummary`,
`NoticeCatalogue`, `ReportBuilder`, `ReportWriter`, `Verbosities`).** These are not false
positives in the analyzer's own terms. I decompiled MA0182 (Meziantou 3.0.341): it counts only
uses inside the declaring assembly and has no exemption for `InternalsVisibleTo`. Each of these
types is used only from Reach.Cli and/or Reach.Tests, through Reach.Core's `InternalsVisibleTo`.
`NoticeCatalogue` is used only by the tests. The analyzer's only escape hatch is
`[DynamicallyAccessedMembers]` on the type, which is a suppression in disguise (ticket 04
rejected it). The code-only options I see:
1. Make the 8 types `public`. No runtime change, but Reach.Core gains a public surface.
2. Move each type to where it is used: the composition types (`ReachPipeline`, `ProcessRunner`,
   `ReportWriter`, ...) move into Reach.Cli, and the tests reach them through Cli's
   `InternalsVisibleTo`. This is a real restructuring.
3. Approve a per-type `SuppressMessage` (or disabling MA0182 for Reach.Core) with the
   justification "used by friend assemblies through InternalsVisibleTo". This is also a likely
   upstream item for the package, which turns on a rule that misfires on any multi-project
   solution that uses InternalsVisibleTo.
Which do you want?

**MA0224 (RespectNullableAnnotations), 3 sites: `ReportWriter.Options`, `ReportTests`,
`ExampleReportTests`.** All three options are serialise-only. Unlike MA0225, this setting
*does* affect serialisation: with `true`, writing a report throws if a non-nullable property
holds null at runtime. Today such a value would be written as `null`. As an experiment I set it
to `true` on `ReportWriter` and the full suite still passed (520/1, the known failure), so no
tested path hits it. But it changes behaviour on untested paths: a nullability bug becomes a
crash, where today it writes a `null` field. Options:
(a) `true` everywhere. Fail fast; a null in a non-nullable report field becomes an internal
    error (exit 70) instead of a report.
(b) `false` explicitly. No behaviour change, and the analyzer accepts any explicit assignment,
    but it may read as a suppression in disguise.
(c) `true` on the two test-only options (dictionary payloads; it has no effect there) and decide
    `ReportWriter` separately.
Which do you want?
