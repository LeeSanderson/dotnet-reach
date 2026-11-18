# 06: Batch: Meziantou and CA rules

**What to build:** Reach's own projects raise no Meziantou (MA) or .NET analyzer (CA) diagnostics under the coding standard. See [the spec](../spec.md), ADR-0016 and ADR-0003.

This is a batch on the integration branch. The build stays red until ticket 09.

- MA0181 / MA0182 (casts) and the other MA and CA rules in ticket 04's list are fixed in code, one commit per rule.
- **Culture rules (CA1305, MA0076 and similar):** name the **invariant** culture, never the current culture, per ADR-0016. The measurement summary line's exact output is ticket 10's job. If the analyzer flags it here, name the invariant culture and leave the percent rendering to ticket 10.
- **Pre-approved suppression:** CA5350 and CA5351 on the SHA1 and MD5 branches of correspondence's document hashing, each as a `SuppressMessage` attribute at that site, justified by: the hash must match the algorithm the compiler recorded in the PDB, and is not used for security. Do not drop SHA1/MD5 support.
- **Any other suppression needs the maintainer's explicit approval for that rule.** Stop and ask, and record the question under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [x] Zero MA and CA diagnostics in Reach's own projects
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

### Resolved: MA0182 and MA0224 (maintainer decisions)

**MA0182.** Maintainer: "The sonar issue is valid and indicative of bad design decision. Remove
the InternalsVisibleTo Reach.Cli and make Reach.Core classes public". (My question set out three
options; the decision was to remove the friend link and make the types public.)

- `Reach.Core.csproj` no longer grants `InternalsVisibleTo` to Reach.Cli. Reach.Tests keeps its
  grant. Reach.Cli's own `InternalsVisibleTo Reach.Tests` is still needed and stays.
- Public now: every Core type Reach.Cli uses, plus every type their public signatures expose.
  Because Cli's public records carry public positional properties, that set reaches most of
  the pipeline's result types. The eight flagged types are public too. Members Cli calls are
  public; everything else keeps its `internal` members.
- Making these public raised three new diagnostics, each fixed in code:
  - S2339: `ReachDirectory.DefaultName` is now a static read-only property instead of a
    public const.
  - S2360 and S1006: the optional `CancellationToken` is now required on
    `IProcessRunner.RunAsync`, `ProcessRunner`, `ReachPipeline.SelectAsync` and the two test
    runners. The two fixture callers that relied on the default pass `CancellationToken.None`,
    which is what the default was.
  - No CS1591 or public-API CA rule fired. Every other diagnostic is the same as before, only
    shifted by column.
- `ScaffoldingTests.Nothing_in_Core_is_public` asserted the M1 scaffolding rule this decision
  reverses. It now asserts that Reach.Core does not grant `InternalsVisibleTo` to Reach.Cli.
  It has its own commit, so it is easy to revert if a different guard is wanted.

**MA0224.** Maintainer: "yes, true everywhere". `RespectNullableAnnotations = true` is set on
`ReportWriter.Options` and on both test options. A null in a non-nullable report property now
fails the run instead of being written as `null`.

After both decisions, a clean (`--no-incremental`) build shows no MA, CA or RS diagnostic in
Reach's own projects. The suite gives 520 passed and 1 failed. The failure is the known ticket
08 acceptance test.
