# Filter dialects and runner detection

Type: research
Status: resolved
Blocked by: (none)

## Question

Reach renders one framework-agnostic selection into whatever **dialect** each test project
needs, and must work out which dialect that is from the compiled output. Both halves are
unknown.

**The dialect matrix.** For each of xUnit (v2, v3 and v4 — confirm v4 exists and what it
is), NUnit, and MSTest, under each runner host they support (VSTest, and
Microsoft.Testing.Platform where applicable):

- The exact filter expression grammar accepted, and how alternatives are combined.
- Escaping rules for names containing commas, parentheses, backticks, generic arity,
  nested types, and non-ASCII characters.
- Whether a **test-method-level** filter matches every case of a parameterised test
  (`[Theory]`, `[TestCase]`, `[DataRow]`), or whether that needs a different operator.
  Selection granularity is the test method (PRD §11), so this is load-bearing: if an
  equality filter misses a theory's generated cases, the selection silently under-selects.
- Any practical ceiling on filter length — command-line limits, and whether a response
  file or `--filter-file` style input exists as an escape hatch.

**Runner detection.** Given a compiled test assembly and its project file, how can Reach
determine which framework and which runner host is in play? Candidate signals: referenced
assembly identities and versions in metadata, the presence of a generated entry point,
`EnableMSTestRunner` / `UseMicrosoftTestingPlatform` style properties, the test SDK package
reference. Which signals are reliable, and which are ambiguous?

**Also worth knowing:** whether any of these runners can be told to skip a test project
entirely, since a project with zero selected tests should not spin up a runner at all.

Findings go to `.scratch/walking-skeleton/research/`. Cite primary sources — official docs
and the frameworks' own repositories — and say plainly where the answer could not be
established rather than inferring it.

## Answer

Full findings: [research/01-filter-dialects-and-runner-detection.md](../research/01-filter-dialects-and-runner-detection.md).

**xUnit v4 does not exist.** The NuGet package `xunit.v3` is at *version* 4.0.0, released
2026-08-14. "v3" is the generation; "4.0.0" is the package version. The confusion is
understandable and the map has been corrected. It matters beyond naming: 4.0.0 changed the
filter surface — it added an MTP `--filter` accepting VSTest syntax, added `-displayName`,
and dropped MTP v1 — so the dialect must be gated on package version, not just on
framework and host.

**The load-bearing question is answered: yes, a method-level filter matches every case of
a parameterised test, in all three frameworks.** Established from adapter and framework
source rather than documentation: xUnit builds `FullyQualifiedName` as `Class.Method` with
no arguments, unchanged across adapters 2.4.5, 2.5.8, 2.8.2 and 4.0.0; MSTest clones each
data row and varies only `DisplayName`; NUnit gets there by a different route. A second
agent reached the same conclusion independently from the same sources.

Counter-intuitively, `DisplayName=` matches **nothing** for a theory — the inverse of what
most people would reach for first.

Every dialect's grammar, three stacked escaping layers, length ceilings, per-host escape
hatches, and the detection signal matrix are in the research file; the detection matrix was
verified empirically against twelve probe builds on SDK 10.0.303.

### The biggest risk: NUnit can under-select silently

NUnit is the one framework whose VSTest `FullyQualifiedName` *includes* the arguments —
`Ns.C.MyTest(1,2)` — so `FullyQualifiedName=Ns.C.MyTest` ought to match nothing. It works
only because the adapter re-parses the filter into NUnit filter XML, where matching is
parent-aware. That chain breaks under `UseNUnitFilter=false`, under
`DiscoveryMethod.Legacy`, and on the IDE path; three historical bug reports show exactly
this failure. Reach cannot see a consumer's `.runsettings`, and cannot detect the failure
either — an under-selected theory simply does not run.

Under the map's widen-when-uncertain rule: **render NUnit with `~` (contains) rather than
equality**, and pin the behaviour with a fixture. Recorded as a decision on the map; the
fixture requirement is carried by
[Fixture catalogue](11-fixture-catalogue.md).

### Two findings for the owner

`--ignore-exit-code 8` **stops working on the .NET 11 SDK** — zero-match handling moved to
a run-level verdict. Emitting no command at all for an empty selection is the only
version-independent answer. Carried by [The report contract](07-the-report-contract.md).

`dotnet test --affected-tests` and `--collect-test-map` exist as hidden, environment-variable
gated options in `dotnet/sdk`, undocumented anywhere. Escalated to
[What is dotnet test --affected-tests](16-what-is-dotnet-test-affected-tests.md), which
resolved it as coverage-based and complementary — and **corrected this finding**: the
options are not in any shipping or public SDK. They exist only in `main` and the
`release/11.0.1xx` branches, not in .NET 11 preview.7, and not in the local SDK 10.0.303.

### Not established

Nineteen items are listed explicitly in the research file rather than guessed. The notable
ones: `--treenode-filter` behaviour with parameterised cases (mitigated — the file
recommends against that dialect on independent grounds); whether NUnit's `--filter` is
case-sensitive, where the source and Microsoft's documentation disagree; and non-ASCII
handling in VSTest filters, where no rule exists in any source.
