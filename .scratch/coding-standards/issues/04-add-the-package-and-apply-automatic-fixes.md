# 04: Add the coding-standards package and apply the automatic fixes

**What to build:** Opinionated.DotNet.CodingStandards applies to every project, and everything `dotnet format` can fix automatically is fixed. What remains is listed by rule, for the batch tickets to work from. See [the spec](../spec.md).

This starts the **integration branch**. With warnings as errors, the build is expected to stay red until ticket 09. Tickets 05–08 are batches on the same branch.

- Add the package to the shared build props as a package reference with all assets private, **pinned to exactly 0.0.22**. Do not introduce Central Package Management.
- Run `dotnet format` (style and analyzers) over Reach's own projects and commit what it fixes automatically: naming such as `_`-prefixed fields, unused usings, whitespace. Leave fixture projects to ticket 08.
- Record the remaining diagnostics, grouped by rule ID with counts, under `## Comments` in this file.

**No suppressions in this or any batch ticket without the maintainer's explicit approval for that rule.** If a fix looks wrong rather than just laborious, stop and ask.

**Blocked by:** 01 (standardise on CRLF)

**Status:** ready-for-agent

- [x] The package is referenced once, from the shared build props, at exactly 0.0.22, with all assets private
- [x] Properties the repository already sets (e.g. `LangVersion`) still take effect
- [ ] The automatic fixes are committed, one commit per rule family where practical
- [x] The remaining diagnostics, by rule ID and count, are recorded under `## Comments`
- [x] Reach's tests still pass with warnings not treated as errors (behaviour is unchanged by the automatic fixes)

## Comments

### Blocker: the pre-commit hook rejects every `.cs` commit until ticket 09

The Husky `format-staged` task runs `dotnet format --verify-no-changes --include <staged>`. That
command exits non-zero for any diagnostic at error severity in a staged file, including ones with no
code fix (checked: `--diagnostics S109` on `ReceiverTypes.cs` exits 2). With the package in, nearly
every Reach `.cs` file carries an unfixable diagnostic, so no auto-fix commit (and no batch-ticket
commit) can pass the hook until the file it touches is fully clean. The package commit itself
(`Directory.Build.props` only) went through. The auto-fix commits below are prepared but not
committed, because skipping hooks is not allowed. **Question for the maintainer:** how should
commits on the integration branch get past the hook while it is red? Options: approve `HUSKY=0` /
`--no-verify` for integration-branch commits until 09; or narrow the hook temporarily (e.g.
`dotnet format whitespace --verify-no-changes` plus `dotnet format style --verify-no-changes`, which
exits 0 on unfixable IDE1006) and restore it in 09.

The prepared commits, in order, each one `dotnet format` run with no hand edits (patches kept by the
implementer; also reproducible by rerunning the command):

1. `dotnet format whitespace` — IDE0055, `CallGraphBuilder.cs` and `ReceiverTypes.cs` (case-block braces)
2. `dotnet format style` — IDE0005 unused usings, 8 lines in 6 files (also clears S1128)
3. `dotnet format style --diagnostics IDE0059 IDE0062 --severity info` — 1 discarded `out var`, 2 static local functions
4. `dotnet format analyzers --diagnostics MA0211 --severity info` — 209 one-line `<summary>`s to multi-line, 85 files
5. `dotnet format analyzers --diagnostics MA0192 --severity info` — 4 bitwise flag tests to `HasFlag` (single-bit flags, equivalent)
6. `dotnet format analyzers --diagnostics MA0243 --severity info` — 4 invisible characters to `\u0001` / `﻿` escapes (same strings)

`--severity info` is needed because `dotnet format` skips these rules at its default severity even
though the build reports them as errors.

### Fixers rejected (output was wrong, not just laborious)

- **CS1573**: inserts empty `<param name="X"></param>` tags and glues the next tag onto the same
  line (`...></param>/// <param name="WillRun">`). Left for 05, which should write real text.
- **SA1143**: collapses multi-line switch expressions and collection expressions onto one 400-column line.
- **MA0182**: adds `[DynamicallyAccessedMembers(All)]` to each type, which is a suppression in disguise.
- **MA0224 / MA0225**: set `RespectNullableAnnotations` / `RespectRequiredConstructorParameters`
  on `JsonSerializerOptions`, which changes deserialisation behaviour. Manual decision for 06.
- **S1172**: removes the parameter from the declaration only, not from call sites.
- **S3240**: if/else to a ternary on one 130-column line. Left for 07.
- **MA0186 / MA0193**: correct but emit fully-qualified `System.Diagnostics.CodeAnalysis...` and
  `global::System.MidpointRounding.ToEven`. One site each, left for 06 to write cleanly.
- **IDE1006, SA1649, MA0190, xUnit2033**: the fixer "doesn't support Fix All in Solution", so
  `dotnet format` cannot apply it. IDE1006 (85 `_` prefixes) is all manual, despite the spec
  expecting `dotnet format` to handle naming.
- Everything else listed below has no code fix.

### Remaining diagnostics after the auto-fixes (492: Core 363, Cli 15, Tests 114)

Fixture projects not counted (not in `Reach.slnx` on this branch; ticket 08).

**05 docs**

| Rule | Count | Projects | Message |
| --- | --- | --- | --- |
| CS1573 | 106 | Core 92, Tests 14 | Parameter has no matching param tag (but other parameters do) |
| CS1572 | 1 | Core 1 | param tag for 'Joined' but no such parameter |
| CS1734 | 1 | Tests 1 | paramref 'lines' on `ChildScript.Chatty()` but no such parameter |

**06 MA / CA**

| Rule | Count | Projects | Message |
| --- | --- | --- | --- |
| MA0181 | 50 | Cli 1, Core 31, Tests 18 | Do not use cast |
| MA0182 | 8 | Core 8 | Internal type apparently never used (`ColourSupport`, `ProcessRunner`, `ReachPipeline`, `HumanSummary`, `NoticeCatalogue`, `ReportBuilder`, `ReportWriter`, `Verbosities`); they look used from Cli/Tests, so likely false positives |
| MA0190 | 5 | Tests 5 | Use partial property instead of partial method for GeneratedRegex |
| MA0191 | 5 | Core 1, Tests 4 | Do not use the null-forgiving operator |
| MA0224 | 3 | Core 1, Tests 2 | Set RespectNullableAnnotations on JsonSerializerOptions |
| MA0225 | 3 | Core 1, Tests 2 | Set RespectRequiredConstructorParameters on JsonSerializerOptions |
| MA0186 | 1 | Core 1 | Equals should use [NotNullWhen(true)] (`MemberKey`) |
| MA0193 | 1 | Core 1 | Math.Round needs a MidpointRounding (`Measurement.cs:41`) |
| RS0030 | 1 | Core 1 | Banned `Math.Round(double, int)`; same site as MA0193 |
| CA1849 | 5 | Tests 5 | `File.WriteAllText` blocks; use the async overload |
| CA1869 | 2 | Tests 2 | Cache and reuse JsonSerializerOptions |
| CA5350 | 2 | Core 1, Tests 1 | Weak hash SHA1 (`Correspondence.cs:170`; `PortablePdbChecksumTests.cs:86`) |
| CA5351 | 1 | Core 1 | Broken hash MD5 (`Correspondence.cs:171`) |
| CA1001 | 1 | Tests 1 | `CommandLineTests` owns disposable fields but is not disposable |

CA5350/CA5351 at `Correspondence.cs:170-171` are pre-approved. **Question for the maintainer:** the
CA5350 in `tests/Reach.Tests/Assemblies/PortablePdbChecksumTests.cs:86` (the test recomputes the
PDB checksum) is outside the pre-approval. Does the same justification cover it?

**07 S / SA and the long tail**

| Rule | Count | Projects | Message |
| --- | --- | --- | --- |
| IDE1006 | 85 | Cli 14, Core 36, Tests 35 | Naming: missing prefix '_' |
| S109 | 42 | Core 42 | Magic number |
| SA1649 | 22 | Core 21, Tests 1 | File name should match first type name |
| S1541 | 14 | Core 14 | Cyclomatic complexity over 10 |
| S3267 | 11 | Core 11 | Loop should use LINQ `Where` |
| S1067 | 10 | Core 10 | More than 3 conditional operators in an expression |
| S3059 | 10 | Core 10 | Member visibility higher than its type's |
| S4017 | 9 | Core 9 | Nested type argument in a method signature |
| S4261 | 9 | Core 1, Tests 8 | Add the 'Async' suffix |
| S4040 | 8 | Core 8 | Normalise with `ToUpperInvariant()` |
| S1227 | 6 | Core 6 | Remove this break statement |
| S6605 | 6 | Core 6 | Use collection `Exists` instead of `Any` |
| S126 | 5 | Core 4, Tests 1 | Missing final 'else' |
| S3776 | 5 | Core 5 | Cognitive complexity over 15 |
| S6966 | 5 | Tests 5 | Await `WriteAllTextAsync` (same sites as CA1849) |
| S1200 | 4 | Core 4 | Class depends on more than 30 types |
| S1301 | 4 | Core 4 | Two-arm switch expression should be a ternary |
| S2325 | 4 | Core 4 | Method can be static |
| S107 | 3 | Core 2, Tests 1 | More than 7 parameters |
| S131 | 3 | Core 3 | Switch statement without 'default' |
| S138 | 3 | Core 3 | Method longer than 80 lines |
| S3358 | 3 | Core 3 | Nested ternary |
| S6602 | 3 | Core 3 | Use `Find` instead of `FirstOrDefault` |
| SA1143 | 3 | Core 2, Tests 1 | Conditional expressions formatted inconsistently (`SynthesisedEdges.cs:332`, `TypeHierarchy.cs:283`, `ReportFixture.cs:79`) |
| S3220 | 2 | Core 2 | Call partially matches a non-params `string.Split` overload |
| S9415 | 2 | Tests 2 | Make local function static |
| IDE0007 | 1 | Core 1 | Use 'var' (`SourceRevision.cs:379`); fixer did not apply |
| S1172 | 1 | Tests 1 | Unused parameter 'documentPath' |
| S1192 | 1 | Core 1 | Literal '--filter' repeated 4 times |
| S127 | 1 | Tests 1 | For-loop stop variable updated in the body |
| S134 | 1 | Core 1 | Control flow nested deeper than 3 |
| S2221 | 1 | Core 1 | Catch a specific exception type |
| S2365 | 1 | Core 1 | Property `Lines` copies a collection |
| S2699 | 1 | Tests 1 | Test has no assertion |
| S3240 | 1 | Core 1 | Use '?:' |
| S3241 | 1 | Tests 1 | Return value never used |
| S3872 | 1 | Tests 1 | Parameter 'read' duplicates the method name |
| S4022 | 1 | Core 1 | Enum storage should be Int32 |
| S4136 | 1 | Core 1 | `Remember` overloads not adjacent |
| S6354 | 1 | Core 1 | Use a testable time provider |
| S6603 | 1 | Core 1 | Use `TrueForAll` instead of `All` |

### Tests

With the auto-fixes applied, `dotnet test --project tests/Reach.Tests -p:TreatWarningsAsErrors=false
-p:RunAnalyzers=false` gives 520 passed, 1 failed (`RunAnalyzers=false` because the package raises
the analyzers to error severity in `.editorconfig`, so `TreatWarningsAsErrors=false` alone does not
let the build through). The failure, `AcceptanceTests.Fact_added_to_an_existing_method_selects_it`,
is not caused by these fixes. It searches the Contoso fixture's `InvoiceTests.cs` for a literal
`"    [Fact]\n    public void ..."`, but after ticket 01 that file checks out with CRLF, so the `\n`
needle no longer matches. It fails on any fresh CRLF checkout of `coding-standards`. It needs a fix
in ticket 01's follow-up or in 08.
