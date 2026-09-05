# Filter dialects and runner detection

Research for [issue 01](../issues/01-filter-dialects-and-runner-detection.md). Investigated
2026-09-05 against primary sources: official Microsoft docs, and the `xunit`, `nunit`,
`microsoft/testfx`, `microsoft/vstest` and `dotnet/sdk` repositories.

Every substantive claim carries a source URL. Claims that could **not** be established are marked
**UNRESOLVED** and say what was checked — per the map's correctness rule, an honest gap is worth
more than a plausible invention. A handful of claims are marked **measured**: they come from
building probe projects against .NET SDK 10.0.303 and reading the resulting PE metadata, and are
stronger than any doc for those specific questions.

---

## 0. Headline findings

1. **There is no xUnit v4.** The confusion is real and understandable: the NuGet package
   `xunit.v3` is at **version 4.0.0**. "v3" is the product generation; "4.0.0" is its package
   version. §1.
2. **`xunit.v3` 4.0.0 shipped 2026-08-14** — three weeks before this research — and it *changed
   the filter surface*. Reach's xUnit dialect must be version-aware. §3.2.
3. **A method-level filter matches every case of a parameterised test in all three frameworks —
   but for three different reasons, and NUnit's reason is fragile.** xUnit and MSTest are safe by
   construction. NUnit is safe only because the adapter re-parses the filter into an NUnit filter
   whose matching is parent-aware; several documented settings turn that off and would cause
   silent **under-selection**. §4 — **this is the single biggest risk**.
4. **The ticket's `UseMicrosoftTestingPlatform` property does not exist.** The real switches are
   `EnableMSTestRunner`, `EnableNUnitRunner`, and `UseMicrosoftTestingPlatformRunner` (xUnit v3
   only). §7.1.
5. **Runner host is a property of the *invocation*, not of the assembly.** It is decided by
   `global.json`'s `test.runner` key — resolved from the **current working directory**, not the
   project directory — plus SDK major version. It cannot be read off compiled output. §7.2.
6. **On the .NET 10 SDK a repository is effectively all-VSTest or all-MTP.** Both mixed directions
   are now hard errors. §7.2.
7. **`dotnet.config` / `[dotnet.test.runner]` is dead** — it existed only in .NET 10 previews and
   was replaced by `global.json` in RC2. §7.2.
8. **A solution-level `dotnet test` with per-project dialects fails.** Under MTP, an option one
   project doesn't recognise fails the run with exit code 5. §7.5.
9. **Escape hatches for long filters exist but every host spells them differently**, and one of
   them (`.runsettings` `<TestCaseFilter>`) has no length limit at all. §6.
10. **`--filter` may be case-*sensitive* for NUnit**, contradicting Microsoft's blanket "all the
    lookups are case insensitive". §3.3 — unresolved, and cheap to settle.
11. **🔴 The .NET SDK is building this feature.** `dotnet test --affected-tests` and
    `--collect-test-map` are hidden, environment-variable-gated options **in the shipping .NET 11
    SDK**. Undocumented; source-verified. **Read §11 before committing to an architecture.**
12. **Zero-match handling changed between the .NET 10 and .NET 11 SDKs**, and `--ignore-exit-code 8`
    stops working on .NET 11. §8.

---

## 1. Does xUnit v4 exist?

**No.** As of 2026-09-05 there are exactly two xUnit core framework lines, v2 and v3.

`https://xunit.net/releases/` has precisely two core-framework headings — "Core Framework v3" and
"Core Framework v2" — and no v4 heading. The v3 line runs
`1.0.0, 1.0.1, 1.1.0, 2.0.0 … 3.2.2, 4.0.0`; v2 runs `2.0.0 … 2.9.3`.
Every GitHub release in the 4.0.0 series is titled **"v3 4.0.0"**, "v3 4.0.0-pre.154", etc.
Sources: <https://xunit.net/releases/>, <https://github.com/xunit/xunit/releases>

| Package | Latest stable | Meaning |
| --- | --- | --- |
| `xunit.v3` | **4.0.0** (published 2026-08-15) | v3 generation, package version 4.0.0 |
| `xunit` (v2 meta-package) | **2.9.3** | v2 generation, maintenance mode |
| `xunit.runner.visualstudio` | **4.0.0** | VSTest adapter; runs "xUnit.net 1.9.2 and later" |
| `xunit.analyzers` | **2.0.0** | |

Sources: <https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json>,
<https://api.nuget.org/v3-flatcontainer/xunit/index.json>,
<https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/index.json>,
<https://www.nuget.org/packages/xunit.runner.visualstudio>

The 4.0.0 notes open: *"Today, we're shipping three new releases: xUnit.net Core Framework v3
`4.0.0` … xUnit.net Analyzers `2.0.0` … xUnit.net Visual Studio adapter `4.0.0`"*, then *"Hello,
4.0! The last major release (3.0.0) was 13 months ago…"*. Date **2026 August 14**.
xUnit v2: *"Core Framework v2 is in maintenance mode. Critical bug fixes will be issued, but no
new feature work is being done."*
Sources: <https://xunit.net/releases/v3/4.0.0>, <https://xunit.net/releases/>

> **Recommendation for the map.** The decision line "Test recognition covers xUnit (v2, v3, v4)"
> should read **"xUnit v2 and v3 (package versions 1.x–4.x)"**. Key the dialect off the *framework
> generation* (from the referenced assembly, §7.3) and then off the *package version* for
> version-gated options — never off a "v4" generation that does not exist.

---

## 2. The VSTest `TestCaseFilter` dialect (shared substrate)

One grammar, used by `dotnet test --filter` (VSTest mode), `vstest.console.exe
--TestCaseFilter:`, and — via the VSTest bridge or an equivalent shim — MTP's `--filter`. Each
adapter only chooses which *properties* it exposes.

### Grammar

`<Property><Operator><Value>[|&<Expression>]`, parenthesisable.

| Operator | Meaning |
| --- | --- |
| `=` | exact match |
| `!=` | not exact match |
| `~` | contains |
| `!~` | doesn't contain |
| `&` | boolean AND |
| `\|` | boolean OR |

- **All lookups are case insensitive** — *"**Value** is a string. All the lookups are case
  insensitive."* (But see §3.3: NUnit's re-parsing may defeat this.)
- No operator ⇒ *contains* on `FullyQualifiedName` (`--filter xyz` ≡ `FullyQualifiedName~xyz`),
  vstest 15.1+.
- **There is no `!` prefix negation.** Negation is only `!=` and `!~`.
- **Precedence: AND binds tighter than OR** — the parser carries the comment
  `// Precedence(And) > Precedence(Or)`.
- `()` (empty parentheses) is a `FormatException`.

Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests>,
<https://github.com/microsoft/vstest/blob/main/docs/filter.md>,
<https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Filter.Source/FilterExpression.cs>

### Properties by framework

| Framework | Supported properties |
| --- | --- |
| MSTest | `FullyQualifiedName`, `Name`, `ClassName`, `Priority`, `TestCategory`, `Id` |
| xUnit | `FullyQualifiedName`, `DisplayName`, `Traits` — **and nothing else** |
| NUnit | `FullyQualifiedName`, `Name`, `Priority`, `TestCategory`, `Category`, `Property` |

Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests>

Confirmed against adapter source, with three dialect subtleties the docs don't spell out:

- **xUnit has no `Name`, no `ClassName`, no `Priority`, and no built-in `Category`.** Its adapter
  special-cases only the literal strings `"FullyQualifiedName"` and `"DisplayName"`, falling
  through to traits. `Category=X` works only because people write `[Trait("Category","X")]`.
  Verified identical on the `v2` branch and `main`.
  <https://github.com/xunit/visualstudio.xunit/blob/main/src/xunit.runner.visualstudio/Utility/TestCaseFilter.cs>
- **MSTest's `Name` *is* xUnit's `DisplayName`.** MSTest registers by VSTest `TestProperty.Label`,
  and `TestCaseProperties.DisplayName` is registered with label `"Name"`:
  `TestProperty.Register("TestCase.DisplayName", NameLabel, …)`. So the same underlying property
  carries two dialect names. NUnit's adapter maps `"Name"` to `TestCaseProperties.DisplayName` too.
  <https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs>,
  <https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.ObjectModel/TestCase.cs>,
  <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/VsTestFilter.cs>
- **xUnit trait filters are unreliable during execution.** The property provider is
  `if (isDiscovery || knownTraits.Contains(name))` — an unknown trait name returns `null` at
  execution time. Root of <https://github.com/xunit/xunit/issues/1314>. Irrelevant if Reach only
  ever filters on names, which it should.

`Id` filtering requires MSTest.TestAdapter 3.6+ and matches the discovered `TestCase.Id` GUID.

### Escaping (filter level)

Escape character `\`; the escapable set from `FilterHelper` is exactly:

```csharp
public const char EscapeCharacter = '\\';
private static readonly char[] SpecialCharacters = ['\\', '(', ')', '&', '|', '=', '!', '~'];
```

`FilterHelper.Unescape` **throws** if `\` is followed by anything outside that set — so do not
backslash-escape a comma or a space at the filter level.
Source: <https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Filter.Source/FilterHelper.cs>

Use the shipped helper rather than hand-rolling:
`Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities.FilterHelper.Escape`, from the
`Microsoft.VisualStudio.TestPlatform.ObjectModel` NuGet package.
Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests>

**Characters outside the escape set:**

| Character | Situation |
| --- | --- |
| `,` (generic type args) | Not a filter special char. Docs prescribe `%2C` on the `dotnet test` path — that is *MSBuild* escaping (below). PowerShell array operator: quote the expression. |
| `"` in a `Name`/`DisplayName` value | Docs prescribe URL encoding: `--filter "Name=MyTestMethod \(%22text%22\)"` — note `%22` **and** backslash-escaped parens. |
| `+` (nested type separator) | Not special. Appears literally: xUnit emits `Ns.Outer+Inner.Method`; NUnit's TSL doc prescribes the same reflection form. |
| `` ` `` (generic arity) | Not special to the filter parser, but **is** PowerShell's escape character — single-quote in PowerShell. Whether any adapter emits a backticked arity is **UNRESOLVED** (§9). |
| `<` `>` | Not special to the filter parser; special to POSIX shells. Quote. |
| Non-ASCII | No documented rule found anywhere. **UNRESOLVED** (§9). |

### The `dotnet test` MSBuild layer — a second, distinct escaping problem

`dotnet test --filter X` does not hand `X` to a process argument. The SDK forwards it as an
**MSBuild property**:

```csharp
public readonly Option<string> FilterOption = new Option<string>("--filter") { … }
    .ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={MSBuildPropertyParser.SurroundWithDoubleQuotes(o!)}");
```

and the VSTest targets pass `VSTestTestCaseFilter="$(VSTestTestCaseFilter)"` to `VSTestTask`.
Sources: <https://github.com/dotnet/sdk/blob/main/src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Test/TestCommandDefinition.VSTest.cs>,
<https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Build/Microsoft.TestPlatform.targets>

That is why the docs prescribe `%2C` for commas — MSBuild escaping, not filter escaping, and only
on the `dotnet test` path.

**So there are three stacked escaping layers and they differ per invocation path:** (1) the shell,
(2) MSBuild (`dotnet test` only), (3) the VSTest filter parser. Invoking `vstest.console.exe` or
the test executable directly removes layer 2.

---

## 3. Per-framework dialects

### 3.1 MSTest

**Under VSTest.** Standard TestCaseFilter grammar. Properties, from
`TestMethodFilter._supportedProperties`: `TestCategory`, `Priority`, `FullyQualifiedName`,
`Name` (= DisplayName), `Id`, `ClassName`; anything unrecognised is looked up in `TestCase.Traits`.
Source: <https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs>

**Under Microsoft.Testing.Platform.** MSTest registers `--filter` with **the same VSTest grammar**
— literally the shared provider the VSTest bridge uses:

```csharp
internal abstract class TestCaseFilterCommandLineOptionsProviderBase : CommandLineOptionsProviderBase
{
    public const string TestCaseFilterOptionName = "filter";
    protected TestCaseFilterCommandLineOptionsProviderBase(IExtension extension, string optionDescription)
        : base(extension, [new CommandLineOption(TestCaseFilterOptionName, optionDescription, ArgumentArity.ExactlyOne, false)]) { }
}
```

with the comment *"Shared by the VSTest bridge and the MSTest adapter's native
Microsoft.Testing.Platform integration so both surface an identical `--filter` option."*
`ArgumentArity.ExactlyOne` — **one** expression, so alternatives must be `|`-joined inside it.
Sources:
<https://github.com/microsoft/testfx/blob/main/src/Platform/SharedExtensionHelpers/TestCaseFilterCommandLineOptionsProviderBase.cs>,
<https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestCaseFilterCommandLineOptionsProvider.cs>

MSTest also honours MTP's platform-native `--treenode-filter` (§3.4). Latest MSTest release number
is **UNRESOLVED** (§9); the source claims here are against `microsoft/testfx` `main`.

### 3.2 xUnit

xUnit has **three** filter front-ends, and they are **mutually exclusive** — `XunitFilters` guards
each with `GuardEmptyQueryFilters()` / `GuardEmptySimpleFilters()` / `GuardEmptyVSTestFilter()`.
The doc says the same: *"you may use either query filters or simple filters, but you may not use
both at the same time."*
Sources: <https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/XunitFilters.cs>,
<https://xunit.net/docs/query-filter-language>

#### (a) v2 and v3 under VSTest — TestCaseFilter grammar

Properties `FullyQualifiedName`, `DisplayName`, trait names. Alternatives combine with `|` in one
expression. **v3 under VSTest accepts exactly the same grammar as v2** — the same adapter serves
v1/v2/v3 and `TestCaseFilter.cs` is behaviourally identical across branches.

#### (b) xUnit v3 native CLI — "simple filters"

Verbatim help text from `CommandLineParserBase.cs`:

```
-class "name"        run all methods in a given test class (type names are fully qualified;
                     i.e., 'MyNamespace.MyClass' or 'MyNamespace.MyClass+InnerClass'; wildcard '*'
                     is supported at the beginning and/or end of the filter)
                       if specified more than once, acts as an OR operation
-class- "name"       ... if specified more than once, acts as an AND operation
-method "name"       run a given test method (including the fully qualified type name;
                     i.e., 'MyNamespace.MyClass.MyTestMethod'; wildcard '*' is supported
                     at the beginning and/or end of the filter)
-namespace "name"    run all methods in a given namespace (i.e., 'MyNamespace.MySubNamespace')
-trait "name=value"  only run tests with matching name/value traits
-filter "query"      use a query filter to select tests
```

Source: <https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Parsers/CommandLineParserBase.cs>

New in **4.0.0**, from `xunit.v3.runner.inproc.console/CommandLine.cs`:

```
-displayName "name"   run all tests with a matching test case display name
-displayName- "name"
-filterVSTest "query" use a VSTest filter to select tests
```

Source: <https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.inproc.console/CommandLine.cs>

> ⚠ **`-filterVSTest` is source-verified but undocumented** — absent from the 4.0.0 release notes
> and from every doc page checked. Its handler calls
> `projectAssembly.Configuration.Filters.SetVSTestFilter(option.Value)`. Do not depend on it.

Composition (from `XunitSimpleFilters`): a `FilterLogicalAnd` of per-kind `FilterLogicalOr`s —
**within a kind, values OR; across kinds, they AND.** So `-method A -method B` selects A ∪ B,
exactly the shape a selection needs. Excludes are `NOT(OR(...))`.
Source: <https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/XunitSimpleFilters.cs>

4.0.0 deprecated the old negation spellings (warnings emitted, old forms still work):
`-noclass`→`-class-`, `-nomethod`→`-method-`, `-nonamespace`→`-namespace-`, `-notrait`→`-trait-`.
Source: <https://xunit.net/releases/v3/4.0.0>

Under MTP the same filters are spelled long-form, and **multiple values may follow one switch**:

| xUnit native | MTP |
| --- | --- |
| `-class` / `-class-` | `--filter-class` / `--filter-not-class` |
| `-method` / `-method-` | `--filter-method` / `--filter-not-method` |
| `-namespace` / `-namespace-` | `--filter-namespace` / `--filter-not-namespace` |
| `-trait` / `-trait-` | `--filter-trait` / `--filter-not-trait` |
| `-displayName` / `-displayName-` (4.0+) | `--filter-display-name` / `--filter-not-display-name` (4.0+) |
| `-filter` (query) | `--filter-query` |
| `-filterVSTest` (4.0+, undocumented) | `--filter` (4.0+) |

> "Filter options in the xUnit.net command line interface must be specified one at a time,
> repeating the filter switch each time. With the Microsoft Testing Platform command line
> interface, multiple filters of the same kind can be specified with just a single switch. For
> example, `-class Foo -class Bar` … can be expressed as `--filter-class Foo Bar`."

Sources: <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>,
<https://github.com/xunit/xunit/blob/main/src/common/MicrosoftTestingPlatform/CommandLineOptionsProvider.cs>

> ⚠ That mapping table on the xUnit site is dated **2026 May 27**, i.e. *before* 4.0.0, with
> samples pinned to `xunit.v3` 3.1.0. It shows the pre-4.0 report switch names and omits
> `-displayName` and `--filter`. Treat 4.0.0's release notes as authoritative over it.

#### (c) xUnit v3 query filter language (v3 only)

Explicitly *"inspired by the MSTest Graph Query Filter"*:

```
/<assemblyFilter>/<namespaceFilter>/<classFilter>/<methodFilter>[traitName=traitValue]
```

- For `MyNamespace.MySubNamespace.MyClass+MySubClass.MyTestMethod` in `MyTests.dll`: assembly
  `MyTests`, namespace `MyNamespace.MySubNamespace`, class `MyClass+MySubClass` (**`+` for nested
  types**), method `MyTestMethod`.
- Must start with `/`; omitted trailing segments are implicit `*`; >4 segments is a parse error.
- `*` only at the **start and/or end** of a segment, never in the middle.
- Within a segment, `(A)|(B)` / `(A)&(B)` — **parentheses are not optional**; mixed operators need
  explicit grouping (`(A)|(B)&(C)` is illegal). Multipart queries cannot span segments.
- Negate with `!` *inside* the parens and *before* the wildcard: `(!*Bar)`, not `!(…)` or `*!`.
  Trait negation must be `[name!=value]`, never `![name=value]`.
- **All string comparisons are case insensitive.**
- **Escaping is hex HTML character references**, not backslashes:
  `&#x21;`=`!`, `&#x28;`=`(`, `&#x29;`=`)`, `&#x2f;`=`/`, `&#x3d;`=`=`, `&#x5b;`=`[`, `&#x5d;`=`]`.
  Implemented as `[GeneratedRegex("&#[xX]([0-9a-fA-F]{1,4});")]` decoded in a loop.
- **Multiple query filters OR together.**

Spelling: `-filter <expr>` in xUnit CLI mode / `dotnet run`; **`--filter-query <expr>`** in MTP
mode / `dotnet test`.

Sources: <https://xunit.net/docs/query-filter-language>,
<https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/QueryFilterParser.cs>

Because the decoder is the same `QueryFilterParser.ToEvaluator` used by the simple filters, the
hex-reference escaping **also applies to `-method` / `-class` / `-namespace` values** — though
this is not documented for them (source-verified via `FilterMethodFullName`).

Which v3 version introduced the query language is **UNRESOLVED** at finer granularity than "all of
v3": the doc says *"New in v3"*, was last updated 2025-03-21 (before v3 1.0.0 GA), and `-filter`
carries no version gate in the parser — unlike `-displayName`, which is gated `Version_4_0_0`.

#### (d) `--filter` on xUnit v3 under MTP — new in 4.0.0

> "We have added `--filter`, which accepts the older VSTest filter syntax. This should assist
> users who are porting from VSTest to Microsoft Testing Platform."

Sources: <https://xunit.net/releases/v3/4.0.0>, <https://github.com/xunit/xunit/issues/3466>

**Version-gated at v3 4.0.0+.** On `xunit.v3 < 4.0.0` under MTP there is no `--filter`; passing it
fails command-line validation with **MTP exit code 5**. Reach must not emit it below 4.0.0.

Note too that `dotnet test` in MTP mode defines **no `--filter` of its own** — it *"forwards any
token it doesn't recognize to the test application"*, and recommends putting app args after a
literal `--`. Source: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp>

#### Other 4.0.0 changes that matter to Reach

- **MTP v1 support removed**; MTP v2 (2.3.3+) required.
- xUnit v3 now sets `<IsTestingPlatformApplication>true</IsTestingPlatformApplication>`.
- New MSBuild properties `$(XunitTestProject)` / `$(XunitTestProjectAOT)`, set by the
  `xunit.v3.core*` packages *"to provide developers with the ability to trigger behavior off
  knowing whether they're being used in the context of an xUnit.net test project"* — the cleanest
  possible detection signal, but only from 4.0.0.
- Native AOT support added.
- Native console runner as a .NET tool: `dotnet tool install -g xunit-console-tool`, run as
  `dotnet xunit-console` (needs .NET 10 SDK+).

Source: <https://xunit.net/releases/v3/4.0.0>

### 3.3 NUnit

**Current versions (2026-09-05).**

| Component | Latest stable | Latest prerelease |
| --- | --- | --- |
| NUnit framework | **4.6.1** (2026-05-19) | 5.0.0-beta.1 (2026-07-04) |
| NUnit3TestAdapter | **6.3.0** (2026-08-24) | — |
| NUnit.ConsoleRunner | **3.22.0** (2026-01-03) | 4.0.0-beta.4 (2026-09-01) |

**NUnit 5 is not shipped as stable** — `https://api.github.com/repos/nunit/nunit/releases` has no
5.0.0 entry; 4.6.1 is the newest tag, and 5.0.0-beta.1 exists only on NuGet. Target NUnit 4.x.
Sources: <https://www.nuget.org/packages/NUnit/>,
<https://api.nuget.org/v3-flatcontainer/nunit/index.json>,
<https://www.nuget.org/packages/NUnit3TestAdapter>,
<https://api.nuget.org/v3-flatcontainer/nunit.consolerunner/index.json>

**Adapter version floors:** **5.0** added MTP (v1.x); **6.0** moved to MTP 2.0, raised the floor to
.NET 8, and fixed several filter-tokenisation bugs (escaped double quotes, an OOM, unrecognised
escape sequences); **6.2.0** made filters respected during `--list-tests` discovery for both
standard and MTP runs. **Prefer adapter ≥ 6.2.0 whenever MTP is in play.**
Source: <https://docs.nunit.org/articles/vs-test-adapter/AdapterV4-Release-Notes.html>

#### Under VSTest — the grammar is VSTest's, but the *matching* is NUnit's

The most important structural fact about the NUnit dialect, and it is in no doc.

The *advertised* property set (`VsTestFilter.SupportedPropertiesCache`):

```csharp
["FullyQualifiedName"] = TestCaseProperties.FullyQualifiedName,
["Name"]               = TestCaseProperties.DisplayName,
["TestCategory"]       = CategoryList.NUnitTestCategoryProperty,
["Category"]           = CategoryList.NUnitTestCategoryProperty,
// plus supported traits: Priority, TestCategory, Category
```

Source: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/VsTestFilter.cs>

But on the command-line path the raw filter *string* is re-parsed into NUnit filter XML:

```csharp
public TestFilter ConvertVsTestFilterToNUnitFilter(IVsTestFilter vsFilter)
{
    …
    var parser = new TestFilterParser();
    var filter = parser.Parse(vsFilter.MsTestCaseFilterExpression.TestCaseFilterValue);
    var tf = new TestFilter(filter);
    …
}
```

Source: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/NUnitTestFilterBuilder.cs>

Mapping actually applied by `TestFilterParser`:

| VSTest property | NUnit element |
| --- | --- |
| `FullyQualifiedName` | `<test>` |
| `Name` | `<name>` |
| `TestCategory` | `<cat>` |
| `Priority` | `<prop name='Priority'>` |
| anything else — **including `Category`** | `<prop name='…'>` |
| bare word, no operator | `<test re='1'>word</test>` (contains) |

| VSTest operator | NUnit XML |
| --- | --- |
| `=` | `<test>value</test>` |
| `!=` | `<not><test>value</test></not>` |
| `~` | `<test re='1'>value</test>` — **a regular expression**, regex-escaped by the adapter |
| `!~` | `<not><test re='1'>value</test></not>` |

Values are XML-escaped (`&`→`&amp;`, `"`→`&quot;`, `<`, `>`, `'`); `~`/`!~` values are additionally
regex-escaped over `` .[]{}()*+?|^$\ ``.
Source: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/TestFilterConverter/TestFilterParser.cs>

⚠ **`Category` is not a case in `TestFilterParser`'s `switch`**, so on the command-line path it
degrades to `<prop name='Category'>` rather than a real `<cat>` filter — even though `VsTestFilter`
advertises it. Emit `TestCategory`, never `Category`.

⚠ **NUnit's tokenizer is stricter than VSTest's.** Word-break set is
`WORD_BREAK_CHARS = "=~!()&|"` plus whitespace, and `UnEscape` only reverses `\(` and `\)`:

```csharp
private string UnEscape(string rhs) => rhs.Replace(@"\(", "(").Replace(@"\)", ")");
```

So a VSTest-legal `\&`, `\|` or `\=` inside a value **breaks the token** in NUnit's parser and
survives as a literal backslash. For `FullyQualifiedName`/`Name` the tokenizer scans a special
`TokenKind.FQN` that accepts balanced parentheses, quoted strings inside them, and `+`/`.` as
segment separators, so `Ns.Outer+Nested.Method(1,"x")` does tokenise.
Source: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/TestFilterConverter/Tokenizer.cs>

⚠ **Case sensitivity — a real discrepancy, UNRESOLVED.** Microsoft says "all the lookups are case
insensitive", but because the adapter hands the expression to NUnit, matching is done by
`ValueMatchFilter`, which does a plain `ExpectedValue == input` and builds its `Regex` **without**
`RegexOptions.IgnoreCase`. That is source-derived; no doc or issue confirming or denying it was
found. **Emit exact-case names.**
Source: <https://github.com/nunit/nunit/blob/main/src/NUnitFramework/framework/Internal/Filters/ValueMatchFilter.cs>

#### Under MTP

Enabled by two project properties, requiring NUnit3TestAdapter **5.0+**:

```xml
<EnableNUnitRunner>true</EnableNUnitRunner>
<OutputType>Exe</OutputType>
```

> "The first property, `EnableNUnitRunner`, enables the MTP. The second enables it to also run as
> an executable… Note that this version can run both with and without MTP."

Implementation is the **VSTest bridge** (`Microsoft.Testing.Extensions.VSTestBridge` is a package
dependency; `NUnitBridgedTestFramework.cs` forwards `request.RunContext` into the same
`NUnit3TestExecutor`). Filter syntax is **`--filter` with the VSTest expression**, documented with
examples:

```
dotnet run --project Contoso.MyTests -- --filter "FullyQualifiedName~UnitTest1|TestCategory=CategoryA"
Contoso.MyTests.exe --filter "FullyQualifiedName~UnitTest1|TestCategory=CategoryA"
```

Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-nunit-runner-intro#tests-filter>,
<https://docs.nunit.org/articles/vs-test-adapter/NUnit-And-Microsoft-Test-Platform.html>,
<https://www.nuget.org/packages/NUnit3TestAdapter>

The adapter takes a different code path here — `ConvertVsTestFilterToNUnitFilterForMTP` — which
first tries a fast path for filters that are purely
`FullyQualifiedName=A|FullyQualifiedName=B|…` (OR-only, `=`-only, no other property) and turns them
into a list of `filterBuilder.AddTest(name)` calls. **That is exactly the shape a Reach selection
renders to.** Otherwise it falls back to `TestFilterParser`.
Sources: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/NUnitTestFilterBuilder.cs>,
<https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/TestFilterConverter/FullyQualifiedNameFilterParser.cs>

> ⚠ **Do not emit `--treenode-filter` for NUnit.** It is an MTP *platform* option registered only
> when a framework calls `AddTreeNodeFilterService()`, and no evidence NUnit does so was found in
> the adapter repo or docs. An unrecognised MTP option fails the run with exit code 5.
> Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>,
> <https://learn.microsoft.com/en-us/dotnet/api/microsoft.testing.platform.helpers.testapplicationbuilderextensions.addtreenodefilterservice>

Historical bug worth knowing: <https://github.com/nunit/nunit3-vs-adapter/issues/1232> —
"Filtering tests doesn't work when using `EnableNUnitRunner`" (adapter 5.0.0-beta.5); the runner
ran all tests and `dotnet test --filter` was ignored. Closed as done.

#### `--where` / the Test Selection Language

NUnit's native filter language. Reachable from the adapter via the `NUnit.Where` runsettings
parameter (`FilterByWhere` → `filterBuilder.SelectWhere`), and from `dotnet test` as
`dotnet test -- NUnit.Where="cat == Urgent or Priority == High"` (note the space after `--`), or in
a `.runsettings` `<NUnit><Where>…</Where></NUnit>`. Added in adapter 3.16.0.

> **Documented caveat, verbatim:** "The `Where` statement does not work for the Visual Studio Test
> Explorer, as it would generate a conflict with the test list the adapter receives. It is intended
> for use with command line tools, `dotnet test` or `vstest.console`."

Source: <https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html#where>

⚠ Source-derived precedence: `filter ??= builder.FilterByWhere(Settings.Where);` — **`NUnit.Where`
is used only when the VSTest filter produced no filter.** Not documented.

**Grammar** (from <https://docs.nunit.org/articles/nunit/running-tests/Test-Selection-Language.html>):

Selectable keywords, verbatim:
- `test` — "The fully qualified test name as assigned by NUnit, e.g. `My.Name.Space.TestFixture.TestMethod(5)`"
- `name` — "The test name assigned by NUnit, e.g. `TestMethod(5)`"
- `class` — "The fully qualified name of the class containing the test"
- `namespace` — "The fully qualified name of the namespace containing the test(s)"
- `method` — "The name of the method, e.g. `TestMethod`"
- `cat` — "A category assigned to the test"
- `id` — "may only be selected using the `==` operator and is intended only for use by programs
  that have explored the tests and cached the ids"
- anything else = a property name (string-valued only)

Operators, verbatim: `==` equality ("a single equal sign (`=`) may be used as well and has the
same meaning"), `!=` inequality, `=~` match a regular expression, `!~` not match a regular
expression. Regex is .NET `Regex.IsMatch`, **unanchored**.

Compound, verbatim: *"Logical and is expressed as `&&`, `&` or `and`. Logical or is expressed as
`||`, `|`, or `or`. The negation operator is `!` and **may only appear before a left
parenthesis**."*

String literals, verbatim: *"The right-hand side of the comparison may be a sequence of non-blank,
non-special characters or a quoted string. Quoted strings may be surrounded by single quotes
(`'`), double quotes (`"`) or slashes (`/`) and may contain any character except the quote
character used to delimit them. If it is necessary to include the quote character in the string,
it may be escaped using a backslash (`\`) as may the backslash itself."* Confirmed against
`Tokenizer.cs` (`if (ch == '\\') ch = GetChar();`).

Namespace caveat, verbatim: *"`namespace == My.Name.Space` … a test
`My.Name.Space.SubNamespace.MyFixture` will not [be selected]"* — use
`namespace =~ ^My\.Name\.Space($|\.)`.

Nested types, verbatim: *"the same format as used for reflection should be used. For example
`My.Name.Space.TestFixture+NestedFixture`"*.

`partition` is accepted by the engine parser (`TestSelectionParser`'s known LHS list is
`"test", "cat", "method", "class", "name", "namespace", "partition", "id"`, and `PartitionFilter`
emits `<partition>{n}/{count}</partition>`) but is **not documented on the TSL page** — do not
rely on it.
Sources: <https://github.com/nunit/nunit-console/blob/main/src/NUnitEngine/nunit.engine/Services/TestSelectionParser.cs>,
<https://github.com/nunit/nunit/blob/main/src/NUnitFramework/framework/Internal/Filters/PartitionFilter.cs>

#### A trap worth recording: `AssemblySelectLimit` defaults to 2000

In the list-based filter path, if the selection exceeds it the adapter **throws the filter away and
runs everything**:

```csharp
public TestFilter FilterByList(IEnumerable<TestCase> testCases)
{
    if (testCases.Count() > settings.AssemblySelectLimit) { … return TestFilter.Empty; }
    …
}
```
```csharp
AssemblySelectLimit = GetInnerTextAsInt(nunitNode, nameof(AssemblySelectLimit), 2000);
```

Documented as: *"If the number of tests exceeds this limit, the list will be skipped and all tests
in the assembly will be run"*. This is **over-selection**, so safe under the correctness rule, but
it silently destroys the benefit for large selections and would confound an over-selection
measurement. Reported in the wild as <https://github.com/nunit/nunit3-vs-adapter/issues/998>.
Sources: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/NUnitTestFilterBuilder.cs>,
<https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/AdapterSettings.cs>,
<https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html#assemblyselectlimit>

#### And a performance cliff

<https://github.com/nunit/nunit3-vs-adapter/issues/1084> — ~860 `FullyQualifiedName~` clauses
against 22,000 tests took **an hour** before execution started. Marked "External". Reach should
treat very long OR-chains as a performance hazard on NUnit, not just a length hazard.

### 3.4 Microsoft.Testing.Platform native dialects

MTP core provides **no `--filter`** — that comes from the VSTest bridge or a framework adapter.
Its own filtering options:

| Option | Since | Notes |
| --- | --- | --- |
| `--treenode-filter <expr>` | — | tree/graph path filter, below. Registered only if the framework calls `AddTreeNodeFilterService()` |
| `--filter-uid <uid…>` | MTP 1.8.0 | filter by test node UID; accepts one or more |
| — | MTP 2.3.0 | `--filter-uid` and `--treenode-filter` **cannot be combined**; doing so fails command-line validation |

Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>

**`--treenode-filter` grammar** ("Graph Query Filtering"):

- Path form `/A/B/C/D`; real shape `/MyAssembly/MyNamespace/MyClass/MyTestMethod*[OS=Linux]`.
- Operators: `&` and, `|` or, `!` unary NOT (**must appear immediately after an opening
  parenthesis**, e.g. `(!A*)`), `()` grouping (**mandatory** when combining conditions),
  `=` equals, `!=` not equal, `*` wildcard.
- `**` matches all nodes at any depth below; `/A/**/B` is deliberately **not** allowed.
- Property filters `[Key=Value]`; wildcards apply to the value only, never the key.
- Only `TestMetadataProperty` entries in a node's `PropertyBag` are matched by `[Key=Value]`;
  traits exposed as other `IProperty` subtypes are silently not matched.

Sources: <https://github.com/microsoft/testfx/blob/main/docs/mstest-runner-graphqueryfiltering/graph-query-filtering.md>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>

**Escaping in `--treenode-filter` is essentially unspecified.** The spec shows only `\*` for a
literal wildcard. The implementation rejects any segment value containing `/`:

```csharp
case ValueExpression vExpr when vExpr.Value.Contains(PathSeparator):
    throw new ArgumentException(… TreeNodeFilterCannotContainSlashCharacterErrorMessage …);
```

No escape mechanism for `(`, `)`, `[`, `]`, `!`, `=` inside a value was found in the spec or the
parser.
Source: <https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs>

> **Recommendation: do not choose `--treenode-filter` as Reach's MTP dialect.** Its escaping is
> undocumented, its behaviour with parameterised cases is **UNRESOLVED** (§9), and NUnit may not
> register it at all. Both MSTest and xUnit v3 expose better-specified alternatives.

---

## 4. The load-bearing question: does a method-level filter match every case of a parameterised test?

**Selection granularity is the test method (PRD §11), so an equality filter that misses a theory's
generated cases would silently under-select.**

| Dialect | Matches all cases? | Confidence | Why |
| --- | --- | --- | --- |
| **xUnit v2/v3 under VSTest**, `FullyQualifiedName=Ns.C.M` | **Yes** | High — adapter source, 4 versions | FQN is `Class.Method` with no arguments |
| **xUnit v2/v3 console**, `-method` / `--filter-method` | **Yes** | High — framework source | filter evaluates `$"{TestClassName}.{TestMethodName}"` |
| **xUnit v3 query filter**, `/*/*/C/M` | **Yes** | High — spec + source | methodFilter matched against the method name |
| **xUnit v3 MTP `--filter`** (4.0+) | **Yes** | High — source | `XunitFilters` maps `FullyQualifiedName` → `TestClassName + "." + TestMethodName` |
| **MSTest under VSTest or MTP**, `FullyQualifiedName=Ns.C.M` | **Yes** | High — adapter source | data rows clone the element and change only `DisplayName` |
| **NUnit under `dotnet test --filter`**, `FullyQualifiedName=Ns.C.M` | **Yes, conditionally** | Medium — framework + adapter source, contradicted by historical bugs | becomes an NUnit `<test>` filter; NUnit's `Pass()` matches parents |
| **NUnit, list-based / legacy discovery path** | **No** | Medium | matches `TestCase.FullyQualifiedName`, which by default *includes* the arguments |
| **nunit-console `--where test == …` / `--testlist`** | **Yes** | Medium — source only, undocumented | same `<test>` / `FullNameFilter` path |
| **`--treenode-filter` (any framework)** | **UNRESOLVED** | — | §9 |

**Any `DisplayName=` filter matches *nothing* for a theory** — the opposite of intuition. §4.1.

### 4.1 xUnit — safe by construction, in every dialect

**VSTest adapter.** `FullyQualifiedName` is `ClassName.MethodName`; the argument-laden name goes to
`DisplayName`:

```csharp
var fqTestMethodName = $"{testCase.TestClassName}.{testCase.TestMethodName}";
var result = new VsTestCase(fqTestMethodName, uri, source) { DisplayName = Escape(testCase.TestCaseDisplayName) };
```

Verified identical across adapter **2.4.5, 2.5.8, 2.8.2 and `main` (4.0.0)** — the whole xUnit v2
era and continuing into v3. There is no argument-appending and no `forceUniqueNames` logic in any
version. All rows of a theory share **one** `FullyQualifiedName`; they are distinguished by
`TestCase.Id` (a GUID from the unique ID) and by `DisplayName`.
Sources:
<https://github.com/xunit/visualstudio.xunit/blob/main/src/xunit.runner.visualstudio/Sinks/VsDiscoverySink.cs>,
<https://github.com/xunit/visualstudio.xunit/blob/2.8.2/src/xunit.runner.visualstudio/Sinks/VsDiscoverySink.cs>,
<https://github.com/xunit/visualstudio.xunit/blob/2.5.8/src/xunit.runner.visualstudio/Sinks/VsDiscoverySink.cs>,
<https://github.com/xunit/visualstudio.xunit/blob/2.4.5/src/xunit.runner.visualstudio/Sinks/VsDiscoverySink.cs>

`TestClassName` is documented as *"the full name of the class where the test is defined (i.e.,
FullName)"* — namespace-qualified, `+` for nested types.
Source: <https://api.xunit.net/v3/4.0.0/Xunit.Sdk.ITestCaseMetadata.html>

**`DisplayName` does carry the arguments**, evidenced by actual runner output in the official docs:

```
MyFirstUnitTests.UnitTest1.MyFirstTheory(value: 6) [FAIL]
```

Source: <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>

⚠ **And `DisplayName` is mangled by the adapter** — `\r`/`\n`/`\t` replaced and truncated:

```csharp
const int MaximumDisplayNameLength = 447;
static string Escape(string value) =>
    Truncate(value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"));
```

So a theory whose display name exceeds 447 characters cannot be matched by `DisplayName=` at all.
It is also affected by the `methodDisplay` / `methodDisplayOptions` configuration, which
`FullyQualifiedName` is not.
Source: as above, plus <https://xunit.net/docs/config-xunit-runner-json>

**xUnit v3 filters.** The method filter evaluates the class+method name, not the case:

```csharp
public sealed class FilterMethodFullName(string filter) : ITestCaseFilter
{
    readonly Func<string?, bool> evaluator = QueryFilterParser.ToEvaluator(filter);
    public bool Filter(string assemblyName, ITestCaseMetadata testCase) =>
        evaluator($"{Guard.ArgumentNotNull(testCase).TestClassName}.{testCase.TestMethodName}");
}
```

The contrasting `FilterDisplayName` evaluates `testCase.TestCaseDisplayName`. `XunitFilters` maps
the VSTest-syntax `FullyQualifiedName` to the same class+method string. **xUnit v2's console and
MSBuild runners behave identically** — `XunitFilters.IncludedMethods` is compared against
`"{ClassName}.{MethodName}"`.
Sources:
<https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/FilterMethodFullName.cs>,
<https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/FilterDisplayName.cs>,
<https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Filtering/XunitFilters.cs>,
<https://github.com/xunit/xunit/blob/v2/src/xunit.runner.utility/Project/XunitFilters.cs>

Corroborated from three independent directions:

- The 4.0.0 notes justify adding `-displayName`/`--filter-display-name` precisely because they
  *"allow… the user to specify an individual theory data row"* — a direct statement that the
  method-name filters operate at method granularity.
- Brad Wilson, in <https://github.com/xunit/xunit/discussions/2938>, directs a user who wants one
  specific theory row to `DisplayName=…` with backslash-escaped parentheses.
- Two independent investigations reached the same conclusion from the same source files.

**Over-match hazards.** `=` is exact and case-insensitive, and xUnit v3's simple filters and query
segments match exactly unless you add `*` (*"`query` means 'match exactly against `query`'"*), so
`-method Ns.C.MyTheory` does **not** also hit `MyTheory2`. The hazard only appears with VSTest's
`~`: `FullyQualifiedName~Ns.C.MyTheory` **would** match `MyTheory2` and `MyTheoryOther`. Since `=`
already works, `~` should never be needed for xUnit.

⚠ **A hazard that remains even with `=`: method overloads.** Two `[Fact]`/`[Theory]` methods with
the same name in the same class share one `FullyQualifiedName`, so no filter can distinguish them
(parameter types live in the separate `ManagedMethod` property, which the grammar does not expose).
Same for `-method`. This is **over-selection** — safe but wasteful.

Nested classes are fine: `Ns.Outer+Inner.Method`, and `+` is special to neither grammar.

### 4.2 MSTest — safe by construction

`TestCase.FullyQualifiedName` comes from `TestMethod.FullyQualifiedName`, a pure function of class
and method name:

```csharp
public string FullyQualifiedName => field ??= $"{FullClassName}.{Name}";
```

and during data-source expansion each row is a **clone** in which only the display name (plus
bookkeeping — `TestCaseIndex`, `DataType`, `SerializedData`, `ActualData`) changes:

```csharp
UnitTestElement discoveredTest = test.Clone();
discoveredTest.TestMethod.DisplayName = displayNameFromTestDataRow
    ?? dataSource.GetDisplayName(methodInfo, d)
    ?? TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, d)
    ?? discoveredTest.TestMethod.DisplayName;
…
discoveredTest.TestMethod.TestCaseIndex = globalTestCaseIndex;
discoveredTest.TestMethod.DataType = DynamicDataType.ITestDataSource;
```

So every `[DataRow]` / `[DynamicData]` case shares one `FullyQualifiedName`:
`FullyQualifiedName=Ns.C.M` selects them all, and `Name=` / `Id=` are the ways to pick one row.
Sources:
<https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTestAdapter.PlatformServices/ObjectModel/TestMethod.cs>,
<https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTestAdapter.PlatformServices/Discovery/AssemblyEnumerator.cs>,
<https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/Extensions/UnitTestElementExtensions.cs>

The docs corroborate: `Id` filtering *"can select one unfolded data-driven iteration without
matching or escaping its display name."*
Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests>

**On `TestIdGenerationStrategy`:** I looked for it and found instead a hash-based
`GenerateSerializedDataStrategyTestId` that folds `TestCaseIndex` into the `Id` for data-driven
tests — which is exactly why `Id` is row-granular while `FullyQualifiedName` is method-granular.
Whether a `TestIdGenerationStrategy` runsettings knob still exists is **UNRESOLVED** (§9), but the
§4.2 conclusion rests on FQN construction, which is independent of ID generation.

### 4.3 NUnit — safe, but via a chain that several settings break

This is where Reach is exposed. NUnit is the one framework whose VSTest
`TestCase.FullyQualifiedName` **does include the arguments** by default:

```csharp
string fullyQualifiedName = testNode.FullName;          // e.g. "Ns.C.MyTest(1,2)"
if (adapterSettings.UseParentFQNForParametrizedTests)
{
    var parent = testNode.Parent;
    if (parent != null && parent.IsParameterizedMethod) { … fullyQualifiedName = parent.FullName; }
}
```

The docs confirm the default shape: *"The default setting is false, causes the VSTest Testcase ID
to be based on the NUnit fullname property… The fullname is also set into the Testcase
FullyQualifiedName property."* And NUnit's FullName for a generated case includes the arguments —
the TSL doc's own example is `My.Name.Space.TestFixture.TestMethod(5)`, and the default name
pattern is `{m}{a}` where `{a}` is *"full argument representation, enclosed in parentheses and
separated by commas"*.
Sources: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/TestConverter.cs>,
<https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html#usenunitidfortestcaseid>,
<https://docs.nunit.org/articles/nunit/running-tests/Template-Based-Test-Naming.html>

`UseParentFQNForParametrizedTests`:

| Attribute | Value |
| --- | --- |
| Location | `<RunSettings><NUnit>…</NUnit></RunSettings>` |
| Type / **default** | bool / **`false`** |
| Introduced | adapter **3.16.1** |

Docs: *"Setting this may give more stable results when you have complex data driven/parametrized
tests."* … *"when this is set… selecting a single test within such a group, means that **all** tests
in that group is executed."* … *"Note that this often has to be set together with
`UseNUnitIdforTestCaseId`."* The adapter's own source comment agrees: *"Note that this also means
you can no longer select a single tests of these to run."* Reported not to take effect from
`nunit.runsettings` in <https://github.com/nunit/nunit3-vs-adapter/issues/730>.
Sources: <https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html#useparentfqnforparametrizedtests>,
<https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/AdapterSettings.cs>

**So why is it still safe on the command-line path?** Because there the filter is not matched
against those FQN strings. It becomes NUnit filter XML (§3.3), where `FullyQualifiedName=X` is
`<test>X</test>` → `FullNameFilter`, and NUnit's matching is **parent- and descendant-aware**:

```csharp
public virtual bool Pass(ITest test, bool negated)
{
    if (negated) return !Match(test) && !MatchParent(test);
    return Match(test) || MatchParent(test) || MatchDescendant(test);
}
public bool MatchParent(ITest test)
    => test.Parent is not null && (Match(test.Parent) || MatchParent(test.Parent));
```

Doc comment, verbatim: *"Determine if a particular test passes the filter criteria. The default
implementation checks the test itself, its parents and any descendants."*

The parent exists and has the right name: a `ParameterizedMethodSuite` is constructed as
`base(method.TypeInfo.FullName, method.Name)` — **FullName = `Ns.C.MyTest`, with no arguments**.
So `<test>Ns.C.MyTest</test>` matches the suite exactly and each generated case passes via
`MatchParent`. `OrFilter.Pass`/`AndFilter.Pass` delegate per child, preserving this.
Sources:
<https://github.com/nunit/nunit/blob/main/src/NUnitFramework/framework/Internal/TestFilter.cs>,
<https://github.com/nunit/nunit/blob/main/src/NUnitFramework/framework/Internal/Filters/FullNameFilter.cs>,
<https://github.com/nunit/nunit/blob/main/src/NUnitFramework/framework/Internal/Tests/ParameterizedMethodSuite.cs>

**The chain that must hold, and what breaks it:**

| Condition | Default | If violated |
| --- | --- | --- |
| `UseNUnitFilter` is `true` | `true` | falls back to `ConvertMsFilterToNUnitFilter`, matching the VSTest expression against `TestCase.FullyQualifiedName` — **under-selects** parameterised cases |
| `DiscoveryMethod` is not `Legacy` | `DiscoveryMethod.Current` | same fallback — **under-selects** |
| Not running from VS Test Explorer | — | the IDE path uses the list-based converter |
| Selection ≤ `AssemblySelectLimit` (2000) | 2000 | filter discarded, everything runs — over-selects (safe) |

Source: <https://github.com/nunit/nunit3-vs-adapter/blob/main/src/NUnitTestAdapter/AdapterSettings.cs>

⚠ **Contrary evidence that must be weighed.** Several historical reports show method-level FQN
filters matching nothing for NUnit parameterised tests:

- <https://github.com/nunit/nunit3-vs-adapter/issues/677> — `--filter
  "FullyQualifiedName=TestFixtureSourceError.TestsBroken.Test1"` → "0 Tests found" with
  `TestCaseSource` (adapter 3.16.0-dev, NUnit 3.12).
- <https://github.com/nunit/nunit3-vs-adapter/issues/607> — "No test matches the given testcase
  filter `FullyQualifiedName=Namespace.Class.Method`" for cases named via `TestCaseData.SetName`
  (adapter 3.12).
- <https://github.com/nunit/nunit3-vs-adapter/issues/919> — `FullyQualifiedName=TestNUnit.Foo.Bar\(1\)`
  matched nothing (adapter 4.1); fixed in 4.2.

These are old adapter versions and some involve custom case names, but they are enough that the
mechanism should not be trusted on assertion alone.

> **This is the biggest correctness risk in the whole dialect matrix.** It is defaults-dependent,
> adapter-version-dependent, invocation-path-dependent, and **silent** when it goes wrong — an
> under-selected theory simply doesn't run. Recommendations in §10.

---

## 5. Escaping — consolidated

| Layer | Applies to | Escape mechanism |
| --- | --- | --- |
| Shell (PowerShell) | everything | quote the whole expression; `,` is the array operator, `;` a statement separator, `@` splat, `` ` `` the escape char |
| Shell (bash/zsh) | everything | quote; `\!` before `!~`; quote `<`, `>`, `,` |
| MSBuild | `dotnet test` only | `%XX` — notably `%2C` for a comma in a generic FQN |
| VSTest filter parser | VSTest / MTP `--filter` | `\` before exactly `` \ ( ) & | = ! ~ ``; use `FilterHelper.Escape` |
| VSTest `Name`/`DisplayName` values | VSTest | URL-encode specials: `%22` for `"`, plus backslash-escaped parens |
| `.runsettings` `<TestCaseFilter>` | VSTest | filter escaping **plus** XML entities (`&`→`&amp;`, `<`→`&lt;`) |
| NUnit's re-parser | NUnit under VSTest/MTP | breaks on `` =~!()&| `` and whitespace; only reverses `\(` and `\)`; values then XML-escaped; `~` values regex-escaped |
| NUnit TSL (`--where`) | nunit-console, `NUnit.Where` | quote with `'`, `"` or `/`; `\` escapes the delimiter and itself. **With `=~`/`!~` the value is a raw regex — escape metacharacters yourself, including `.` in namespaces** |
| xUnit query + simple filters | `-filter`, `--filter-query`, `-method`, … | hex HTML refs, e.g. `&#x28;` for `(`. Documented for the query language; source-verified to apply to simple filters too |
| MTP `--treenode-filter` | any MTP framework | `\*` for a literal `*`; `/` rejected outright; nothing else documented |

**Notes on the xUnit hex scheme** — the cleanest escaping story of any dialect, and the documented
mechanism for *"any other character your terminal might not directly support"*, so it also handles
non-ASCII. But: `&`, `|` and `*` are **not** in the doc's list despite being grammar operators, so
`&#x26;` / `&#x7c;` / `&#x2a;` are inferred, not documented (**UNRESOLVED**, §9). Escape a literal
`&` as `&#x26;` regardless — the decoder is a plain regex loop and would mis-decode a test name
containing `&#x…;`. Only 1–4 hex digits (one UTF-16 code unit), so astral-plane characters need a
surrogate pair; whether that round-trips is **UNRESOLVED**.

**Practical implication:** `FilterHelper.Escape` covers the VSTest layer correctly, but nothing
covers the MSBuild layer or NUnit's stricter re-parser. Reach should escape for the VSTest layer,
`%2C`-encode commas when the path is `dotnet test`, and prefer invoking a test project or
executable directly for a predictable escaping story.

---

## 6. Filter length ceilings and file-based escape hatches

### Hard limit

Windows `CreateProcessW`: *"The maximum length of this string is 32,767 characters, including the
Unicode terminating null character."*
Source: <https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw>

xUnit states the same limit in its own docs: *"In the rare case that your command line exceeds the
32K character limit in Windows, you can use a 'response file'…"*
Source: <https://xunit.net/docs/getting-started/v3/getting-started>

And `cmd.exe` is tighter still: *"The maximum length of the string that you can use at the command
prompt is 8191 characters."* Its documented workaround is exactly the shape Reach needs: *"Modify
programs that require long command lines so that they use a file that contains the parameter
information, and then include the name of the file in the command line."*
Source: <https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/command-line-string-limitation>
(KB 830473)

No test framework or runner documents a maximum *filter expression* length. Treat **8,191** as the
ceiling whenever a command may go through `cmd`, and 32,767 otherwise.

### Escape hatches, by host — the spellings all differ

| Host | Mechanism | Format |
| --- | --- | --- |
| **xUnit v3 native CLI** / `xunit-console` | `@@ <file>` | ⚠ `@@` is a **separate token followed by the filename**, and *"the command line must contain only `@@ filename`"*. One argument per line, **no quoting**. |
| **Microsoft.Testing.Platform** (direct exe) | `@<file>` | *"The response file name must immediately follow the `@` character with no white space."* Parsed like a command line (whitespace-separated, quoting supported). Combinable with inline args: `./TestExecutable.exe @"filter.rsp" --timeout 10s` |
| **`dotnet test`** (MTP mode) | `@<file>` | ⚠ *"the SDK command-line parser uses a token-per-line approach where each line in the response file is treated as a single token. In that case, each argument must be on a separate line."* |
| **`vstest.console.exe`** | `@<file>` | *"Reads additional options from the specified response file. Arguments in the file are separated by whitespace (spaces or newlines) and quoting is supported."* |
| **`dotnet test`** (VSTest mode) | **Effectively none** | `dotnet test` doesn't recognise a response file for a DLL-based run, and `dotnet vstest` expands it back onto the command line, reintroducing the 32k limit. Issue **closed as not planned**. |
| **`nunit-console`** | `@<FILE>` | *"Specifies the name (or path) of a FILE containing additional command-line arguments… **Each line in the file represents one argument**. If an option takes a value, **that value must appear on the same line**."* |

Sources: <https://xunit.net/docs/getting-started/v3/getting-started>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>,
<https://learn.microsoft.com/en-us/visualstudio/test/vstest-console-options>,
<https://github.com/microsoft/vstest/issues/3513>,
<https://docs.nunit.org/articles/nunit/running-tests/Console-Command-Line.html>

### The best VSTest escape hatch: `.runsettings` `<TestCaseFilter>` — it exists

```xml
<RunConfiguration>
  <TestCaseFilter>(TestCategory != Integration) &amp;amp; (TestCategory != UnfinishedFeature)</TestCaseFilter>
</RunConfiguration>
```

Documented as *"A filter expression in the format `<property><operator><value>[|&<Expression>]`.
The boolean operator `&` should be represented by the HTML entity `&amp;`. Expressions can be
enclosed in parentheses."* Supplied via `dotnet test --settings file.runsettings` or
`vstest.console.exe /Settings:file.runsettings`. **No command-line length limit applies.**
Source: <https://learn.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file>

**And it is maintainer-tested at exactly Reach's scale.** VSTest maintainer nohwnd, on the issue
that motivated the feature: *"Runsettings in the linked PR are the way forward. Command line size
limits are set by OS and we cannot do anything about that… I was able to test this successfully
with 10k and 65k tests, that I all chose by the fully qualified name filter. **The filter size was
600k chars for the 10k run, and some 3,500k for the 65k run.**"*
Sources: <https://github.com/microsoft/vstest/issues/2357>,
<https://github.com/microsoft/vstest/pull/2356> ("Take TestCaseFilter from runsettings"). The
release it first shipped in is **UNRESOLVED** (§9); the feature's existence is confirmed.

⚠ Escaping is doubled here: filter-grammar backslashes **plus** XML entities.

⚠ **A pre-existing `<TestCaseFilter>` in the consumer's runsettings is AND-ed with the one Reach
supplies**, on both hosts. Reach must detect an existing runsettings filter rather than assume its
own is the only one in play — an AND with someone else's category filter is a silent
**under-selection**.

### The best nunit-console escape hatch: `--testlist`

- **`--testlist=PATH`** — *"The name (or path) of a FILE containing a list of tests to run or
  explore, **one per line**. May also include **comment lines, indicated by `#` in the first
  column**."*
- `--test=NAMES` — comma-separated FULLNAMEs, repeatable; *"retained for backward compatibility"*.

`TestFilterBuilder.AddTest(string fullName)` — doc comment *"The full name of the test, as created
by NUnit"* — accumulates into `<filter><test><![CDATA[name]]></test>…</filter>`, i.e. **the same
`<test>` / `FullNameFilter`** as `--where test ==`. So per §4.3, a testlist line naming the
parameterised *method* selects all its generated cases, and a line naming one case selects just
that case. The `<filter>` root ORs its children, so `--testlist` and `--where` combine with OR.
**Source-derived; not stated in the docs — verify empirically.**
Sources: <https://docs.nunit.org/articles/nunit/running-tests/Console-Command-Line.html>,
<https://github.com/nunit/nunit-console/blob/main/src/NUnitEngine/nunit.engine/Services/TestFilterBuilder.cs>

### Other file-shaped inputs

- `vstest.console.exe /Tests:<name1>,<name2>` — matches against the full test name including
  namespace. **Cannot be combined with `/TestCaseFilter`**, and is a command-line option, not a
  file, so it does not relieve the ceiling. Its semantics for parameterised cases are
  **UNRESOLVED** (§9).
- `vstest.console /ListTests` — note *"The `/TestCaseFilter` option has no effect when listing
  tests; it only controls which tests get run."*

---

## 7. Runner detection

### 7.1 MSBuild properties that select the runner host

| Property | Real? | Values / default | Owner |
| --- | --- | --- | --- |
| `EnableMSTestRunner` | ✅ | `true`/`false`, default unset ⇒ VSTest | MSTest **3.2+**; set automatically by `MSTest.Sdk` |
| `EnableNUnitRunner` | ✅ | `true`/`false`, default unset | `NUnit3TestAdapter` **5.0+** |
| `UseMicrosoftTestingPlatformRunner` | ✅ | `true`/`false` | **xUnit v3 only** — replaces xUnit's console UX with MTP's |
| **`UseMicrosoftTestingPlatform`** | ❌ **does not exist** | — | The ticket named it; no primary source has it. The three above are the real switches. |
| `TestingPlatformDotnetTestSupport` | ✅ | `true`/`false`, **default `false`** | `Microsoft.Testing.Platform.MSBuild` — redirects the `VSTest` target to `InvokeTestingPlatform` |
| `TestingPlatformCommandLineArguments` | ✅ | free-form arg string | MTP.MSBuild — works in **both** `dotnet test` modes |
| `IsTestingPlatformApplication` | ✅ | **default `true` when `Microsoft.Testing.Platform.MSBuild` is referenced** | MTP.MSBuild — the master "this is an MTP app" switch |
| `GenerateTestingPlatformEntryPoint` | ✅ | default = `$(IsTestingPlatformApplication)` | MTP.MSBuild |
| `IsTestProject` | ✅ | `true`, set by `Microsoft.NET.Test.Sdk` | VSTest — *"signifies whether a project is a VSTest test project so that it's recognized by `dotnet test`"* |
| `GenerateProgramFile` | ✅ | `true` | `Microsoft.NET.Test.Sdk` — the **VSTest** entry-point generator |
| `UseVSTest` | ✅ (`MSTest.Sdk` only) | `true` ⇒ VSTest instead of MTP | MSTest.Sdk |
| `IsTestApplication` | ✅ (`MSTest.Sdk` only) | `false` for helper libraries | MSTest.Sdk |
| `XunitTestProject` / `XunitTestProjectAOT` | ✅ (xUnit v3 **4.0+**) | `true` | set by `xunit.v3.core*` so tooling can detect an xUnit test project |
| `TestingPlatformShowTestsFailure` / `TestingPlatformCaptureOutput` | ✅ | show `false`, capture `true` | MTP.MSBuild — **ignored** in .NET 10 MTP mode; docs say delete on migration |

Sources: <https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-sdk>,
<https://docs.nunit.org/articles/vs-test-adapter/NUnit-And-Microsoft-Test-Platform.html>,
<https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>,
<https://xunit.net/releases/v3/4.0.0>

MTP opt-in also wants `<OutputType>Exe</OutputType>` — the migration guide says *"For all test
frameworks, add `<OutputType>Exe</OutputType>` to all test projects."*
Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform>

### 7.2 Which mode `dotnet test` is actually in

Two modes, and the discriminator is **not** in the project file:

- **VSTest mode** — the default, and the only mode before the .NET 10 SDK.
- **MTP mode** — introduced in the **.NET 10 SDK**, requires MTP **1.7+**, enabled by
  `global.json`:

  ```json
  { "test": { "runner": "Microsoft.Testing.Platform" } }
  ```

  `test.runner` is a documented `global.json` field, *"Available since: .NET 10.0 SDK"*; the other
  valid value is `VSTest`, which is the default.

Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>,
<https://learn.microsoft.com/en-us/dotnet/core/tools/global-json>,
<https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test>

**`dotnet.config` / `[dotnet.test.runner]` is dead.** It existed only in .NET 10 previews up to RC1
and was replaced by `global.json` in RC2: *"The dotnet.config file (which used an INI-like format)
is no longer supported in the net10 rc2 release of MTP. This is a breaking change."*
**Measured:** with only a `dotnet.config` present on SDK 10.0.303, `dotnet test --project …` fell
through to VSTest mode and failed with `MSBUILD : error MSB1001: Unknown switch … --project`.
Source: <https://github.com/dotnet/sdk/issues/51283>

> ⚠ **`global.json` is resolved from the current working directory, not the project directory.**
> *"**.NET SDK muxer** handles `dotnet` CLI commands. It starts from the **current working
> directory**, which isn't necessarily the same as the project directory."* **Measured:** the same
> command failed from a repo root and succeeded from the probe directory. **Reach must resolve
> `global.json` by walking up from the directory `dotnet test` will be invoked in.**
> Source: <https://learn.microsoft.com/en-us/dotnet/core/tools/global-json>

**Detection procedure:** walk up from the intended CWD for `global.json`, read `$.test.runner`
(absent or `"VSTest"` ⇒ VSTest mode); then check SDK major version — `< 10` means MTP mode does not
exist.

**On .NET 10 a repository is effectively all-VSTest or all-MTP.** Both mixed directions are hard
errors. **Measured**, running an MTP app through VSTest mode:

```
Microsoft.Testing.Platform.MSBuild.targets(355,5): error : Testing with VSTest target is no longer
supported by Microsoft.Testing.Platform on .NET 10 SDK and later. …
```

The guard is `_SdkMajorVersion >= 10 AND IsTestingPlatformApplication == true`, and the docs
confirm support *"will be removed in MTP version 2 if run with .NET 10 SDK"*. Conversely, in MTP
mode a VSTest-only project is a hard failure — **measured**:

```
global.json defines test runner to be Microsoft.Testing.Platform. All projects must use that test runner.
The following test projects are using VSTest test runner: mstest-vstest.csproj
```

xUnit 4.0.0's release notes carry the same warning text for its users. Sources: as above, plus
<https://xunit.net/releases/v3/4.0.0>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/test-platforms-overview>

In MTP mode the CLI surface changes too: `--solution X.sln`, `--project X.csproj`,
`--test-modules <glob>` replace the positional forms.

**Current SDK context:** .NET 10 is GA/LTS; .NET 11 is in preview (Preview 7, Aug 2026; GA
scheduled 2026-11-10). Sources: <https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/>,
<https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

### 7.3 Signals from compiled assembly metadata

**Framework identity — the reliable `AssemblyRef` markers** (measured against probe builds):

| Framework | AssemblyRef name | AssemblyVersion stamped |
| --- | --- | --- |
| xUnit **v2** | `xunit.core`, `xunit.assert` | tracks package (2.9.3 ⇒ `2.9.3.0`) |
| xUnit **v3** | `xunit.v3.core`, `xunit.v3.assert` (+ `xunit.v3.common`, `xunit.v3.runner.common`, `xunit.v3.runner.inproc.console`) | 4.0.0 ⇒ `4.0.0.0` |
| NUnit 3 | `nunit.framework` | 3.14.0 ⇒ `3.14.0.0` |
| NUnit 4 | `nunit.framework` **and** `nunit.framework.legacy` (legacy only if classic asserts used) | 4.6.1 ⇒ `4.6.0.0` |
| MSTest **≤ 3.x** | `Microsoft.VisualStudio.TestPlatform.TestFramework` | **always `14.0.0.0`** |
| MSTest **4.x** | **`MSTest.TestFramework`** | real version (`4.4.0.0`) |

> ### 🔴 **MSTest v4 renamed its assemblies.**
> `Microsoft.VisualStudio.TestPlatform.TestFramework` → **`MSTest.TestFramework`**, and
> `Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter` → **`MSTest.TestAdapter`**. The boundary
> is exactly **3.11.0 → 4.0.0** (measured by extracting both `.nupkg`s and reading `lib/net8.0`).
> Also: MSTest 3.x pins AssemblyVersion to `14.0.0.0`, so **you cannot infer an MSTest 3.x version
> from AssemblyVersion**. Motivation: <https://github.com/microsoft/testfx/issues/5690>. The
> [v3→v4 migration doc](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-v3-v4)
> says *"MSTest v4 isn't binary compatible with MSTest v3"* but does **not** spell the rename out —
> **the rename is UNVERIFIED in Microsoft prose, verified in the shipped packages.**

Note `xunit.core.dll` ships in the **`xunit.extensibility.core`** package (`xunit.core` is a
metapackage), and `xunit.v3.core.dll` in **`xunit.v3.extensibility.core`**. `xunit.abstractions` is
pinned at 2.0.3 and usually only referenced if you use `ITestOutputHelper` — **not a reliable
marker**. Source: <https://xunit.net/docs/nuget-packages-v3>

**Runner-capability markers:**

- **`Microsoft.Testing.Platform` in the test assembly's own `AssemblyRef` table ⇒ MTP is wired in.**
  Reliable, because MTP's MSBuild package compiles a generated `Main` into your assembly that calls
  `Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync`.
- **`testhost.dll` / `testhost.exe` beside the test assembly ⇒ VSTest-capable.** Correlated 1:1
  with a `Microsoft.NET.Test.Sdk` reference across 12 probes. This is a *bin-directory* signal, not
  metadata.
- **`Microsoft.Testing.Extensions.VSTestBridge` is NOT usable.** It lands in `bin/` for NUnit and
  MSTest 3.x *even in pure VSTest configurations*, is **never** in the test assembly's
  `AssemblyRef` table, and MSTest 4.x didn't ship it at all in the probes.
- **`<AssemblyName>.testconfig.json` in the output dir is NOT a general MTP marker** — it is only
  emitted when a source `testconfig.json` exists or `TestingPlatformCommandLineOptionDefault` items
  are present.

**Measured probe matrix** (all `net10.0`):

| Probe | MTP in AssemblyRefs? | `testhost.dll` in bin? |
| --- | --- | --- |
| xunit v2 + Test.Sdk | ❌ | ✅ |
| xunit v3 (`xunit.v3` 4.0.0) | ✅ | ❌ |
| xunit v3 + `xunit.runner.visualstudio` + Test.Sdk | ✅ | ✅ ← **both** |
| xunit v3 `mtp-off` + Test.Sdk | ❌ | ✅ |
| NUnit 4 + adapter 6.3 + Test.Sdk | ❌ | ✅ |
| NUnit 4 + `EnableNUnitRunner` | ✅ | ❌ |
| MSTest 4.4 + Test.Sdk | ❌ | ✅ |
| MSTest 4.4 + `EnableMSTestRunner` | ✅ | ❌ |
| MSTest 3.11 + `EnableMSTestRunner` | ✅ | ❌ |
| `MSTest.Sdk/4.4.0` | ✅ | ❌ |

### 7.4 Signals that are worthless or ambiguous

**"It's an EXE with an entry point" proves nothing.** `Microsoft.NET.Test.Sdk.targets` has, and has
had for years:

```xml
<!-- Output type for .NET Core test projects should be exe. -->
<OutputType Condition="…">Exe</OutputType>
…
<GenerateProgramFile Condition="'$(GenerateProgramFile)' == ''">true</GenerateProgramFile>
```

and `Microsoft.NET.Test.Sdk.Program.cs` is literally
`class AutoGeneratedProgram {static void Main(string[] args){}}`. **Measured:** all 12 probes —
including plain xUnit v2 + VSTest — had `PEHeaders.IsExe == true` and a non-zero entry-point token;
only a control class library did not. **Do not use `OutputType=Exe`, COFF characteristics, or
entry-point presence to infer anything.**

Three generators compile an entry point *into* the test assembly:

| Generator | Emitted file | Trigger |
| --- | --- | --- |
| `Microsoft.NET.Test.Sdk` | *(none on disk — compiles the package's own `Program.cs`)* | `GenerateProgramFile=true` |
| `Microsoft.Testing.Platform.MSBuild` (an MSBuild **task**, not a source generator) | `MicrosoftTestingPlatformEntryPoint.cs`, `SelfRegisteredExtensions.cs` | `GenerateTestingPlatformEntryPoint` |
| `xunit.v3.msbuildtasks` | `XunitAutoGeneratedEntryPoint.cs` (+ `SelfRegisteredExtensions.cs` when MTP on) | always, for xUnit v3 |

⚠ **xUnit v3's generated `Main` is a runtime *branch*** — with `xunit.v3.mtp-v2`:

```csharp
if (Enumerable.Any(args, arg => arg == "--server" || arg == "--internal-msbuild-node"))
    return Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(...);
else
    return Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args)...;
```

`UseMicrosoftTestingPlatformRunner=true` **inverts** the branch. So for xUnit v3, "references
`Microsoft.Testing.Platform`" means **MTP-capable**, not "MTP is the default host" — and the
distinguishing property is not recoverable from assembly metadata.

**A project can reference more than one test framework** — measured: a project referencing
`xunit` 2.9.3 + `NUnit` 4.6.1 + both adapters compiled cleanly, with refs
`xunit.core, xunit.assert, nunit.framework`. **Reach must treat "framework" as a set, not a
scalar**, and fall back to **whole-project selection** when it cannot pick one.

**A project can support both hosts.** xUnit says explicitly *"Supporting VSTest is separate from
(and does not interfere with) our support for Microsoft Testing Platform."* Microsoft says *"By
setting `EnableMSTestRunner` or `EnableNUnitRunner` … your test project will support both VSTest
and MTP. In that scenario, if you use the VSTest mode of `dotnet test` and do not set
`TestingPlatformDotnetTestSupport` to true, you are essentially running entirely with VSTest, as if
`EnableMSTestRunner` and `EnableNUnitRunner` are not set to true."* (Measured caveat: for
MSTest/NUnit the dual case needs `Microsoft.NET.Test.Sdk` kept — `EnableMSTestRunner=true` alone
produced no `testhost.dll`.)

**`Microsoft.NET.Test.Sdk` is a decent VSTest signal, and its absence is a solid "not VSTest"** —
but MSTest v4 removed it: *"MSTest.Sdk no longer adds `Microsoft.NET.Test.Sdk` reference when using
MTP … This package reference is unnecessary when running with MTP and has been removed in MSTest
v4."* And a user can set `IsTestProject` by hand, so the **property** is authoritative and the
package is a proxy.
Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-v3-v4>

**Use the SDK's own definition.** `Microsoft.NET.Sdk.targets` (SDK 10.0.303, line 1313):

```xml
<!-- NOTE: A VSTest non-MTP test project will have IsTestProject=true AND IsTestingPlatformApplication!=true -->
<_IsVSTestTestProject Condition="'$(IsTestProject)' == 'true' and '$(IsTestingPlatformApplication)' != 'true'">true</_IsVSTestTestProject>
```

**Measured property matrix** (via `dotnet msbuild -getProperty:…`):

| Probe | `IsTestProject` | `IsTestingPlatformApplication` | verdict |
| --- | --- | --- | --- |
| plain library | *(empty)* | *(empty)* | not a test project |
| xunit v2 + Test.Sdk | `true` | *(empty)* | VSTest |
| xunit v3 (MTP default) | *(empty)* | `true` | MTP |
| xunit v3 + VSTest adapter | `true` | `true` | **both** |
| xunit v3 `mtp-off` | `true` | *(empty)* | VSTest |
| NUnit VSTest | `true` | `false` | VSTest |
| NUnit `EnableNUnitRunner` | *(empty)* | `true` | MTP |
| MSTest VSTest | `true` | `false` | VSTest |
| MSTest `EnableMSTestRunner` | *(empty)* | `true` | MTP |
| `MSTest.Sdk` | *(empty)* | `true` (+ `UsingMSTestSdk=true`, `UseVSTest=false`) | MTP |

Two more ambiguities: **xUnit v2 + MTP exists only via a third party** (`YTest.MTP.XUnit2`, *"not
officially supported by xUnit.net or Microsoft"*) — treat xUnit v2 + an MTP reference as "could not
establish"; and the xUnit v3 `mtp-v1` / `mtp-v2` / `mtp-off` package variants change whether MTP is
referenced at all (`mtp-v2` is the default for `xunit.v3` 4.0.0).
Sources: <https://xunit.net/docs/getting-started/v2/microsoft-testing-platform>,
<https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>

### 7.5 The mixed-solution problem — the practical blocker for "one dialect per project"

Reach's plan is to detect framework and runner **per test project** and render each dialect. Under
MTP that is exactly the configuration Microsoft documents as failing:

> "When a solution contains test projects that use different test frameworks (for example, MSTest
> and xUnit.net) or different sets of extensions, running `dotnet test` with framework-specific or
> extension-specific command-line options can fail. Options that are valid for one project are
> unrecognized by another, causing **exit code 5** (invalid command-line arguments). For example:
> xUnit.net uses `--filter-trait` while MSTest uses `--filter`, and each framework rejects the
> other's options."

The documented solution is **`TestingPlatformCommandLineArguments` routed by MSBuild condition**:

```xml
<PropertyGroup>
  <TestingPlatformCommandLineArguments
    Condition="'$(UsingMSTestSdk)' == 'true'"
    >$(TestingPlatformCommandLineArguments) $(MSTestSpecificArgs)</TestingPlatformCommandLineArguments>
  <TestingPlatformCommandLineArguments
    Condition="'$(UsingMSTestSdk)' != 'true'"
    >$(TestingPlatformCommandLineArguments) $(XUnitSpecificArgs)</TestingPlatformCommandLineArguments>
</PropertyGroup>
```

```dotnetcli
dotnet test -p:MSTestSpecificArgs="--filter FullyQualifiedName~IntegrationTests" -p:XUnitSpecificArgs="--filter-trait Category=Integration"
```

Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>

For the VSTest-mode case the docs simply say *"it is recommended to avoid including both VSTest and
MTP in the same solution. This scenario is not officially supported."*

> **Implication.** Reach has three viable shapes, in increasing order of robustness:
> **(a)** emit one command **per test project** (invoke the project, its executable, or
> `vstest.console.exe` directly) — cleanest, sidesteps mixed-solution failures, gives a predictable
> escaping story, and makes "skip an empty project" trivial;
> **(b)** emit a solution-level `dotnet test` plus a `Directory.Build.props` snippet using
> `TestingPlatformCommandLineArguments` — the documented pattern, but requires Reach to write into
> the user's repo, which sits badly with ADR-0001's "persists no state" spirit;
> **(c)** a solution-level command with one filter — safe only in a single-framework solution.
> **The report contract (issue 07) should be able to express (a).**

---

## 8. Can a runner be told to skip a project / tolerate zero selected tests?

| Host | Zero selected tests → exit code | Escape hatch |
| --- | --- | --- |
| **xUnit v3 native CLI** / `xunit-console` | **0** | none needed |
| **VSTest** (`dotnet test` VSTest mode, `vstest.console`) | **0** with a warning | `<TreatNoTestsAsError>true</TreatNoTestsAsError>` opts *into* failure |
| **nunit-console** | **0** (source-derived) | none needed; `-2` is for an invalid/empty *assembly*, not a filter |
| **MTP executable, run directly** | **8** (zero tests) or **9** (below `--minimum-expected-tests`) | `--ignore-exit-code 8` |
| **`dotnet test` MTP mode, .NET 10 SDK** | **8** per module ⇒ whole run fails | `--ignore-exit-code 8` **works** |
| **`dotnet test` MTP mode, .NET 11 SDK** | per-module 8 normalised away; fails only if **every** targeted project matched nothing | `--ignore-exit-code 8` **does NOT work** — see below |
| **`dotnet test` MTP mode, zero *projects*** | **1** | must be special-cased by the caller |

### xUnit v3 native CLI — the cleanest story

Source-verified: `return project.Configuration.IgnoreFailures == true || failCount == 0 ? 0 : 1;`
— with zero tests, `failCount == 0`, so exit **0**. There is no "no tests found" error path and no
`-failIfNoTests` flag.
Source: <https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.inproc.console/ConsoleRunner.cs>

### VSTest — tolerant by default

> "By default, the command returns 0 when it exits normally, even if no tests are discovered…
> When discovery finds no matching tests, the runner prints a *warning* rather than an error, and
> by default still returns `0`. To make a run that discovers or selects zero tests return `1`
> instead, set `<TreatNoTestsAsError>true</TreatNoTestsAsError>` in the **RunConfiguration**
> element of your *.runsettings* file."

Sources: <https://learn.microsoft.com/en-us/visualstudio/test/vstest-console-options>,
<https://learn.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file>

Exit codes are only 0 or 1 — *"The process never returns any other value."*

`TreatNoTestsAsError` was added in Microsoft.TestPlatform **16.9** (PR
<https://github.com/microsoft/vstest/pull/2610>, driven by
<https://github.com/microsoft/vstest/issues/2590> and
<https://github.com/dotnet/sdk/issues/13942>) and was **renamed from `FailWhenNoTestsFound`**
before shipping — that older name is invalid. It can also be set **from the command line** as an
inline runsettings override, without a file:

```
vstest.console.exe test.dll -- RunConfiguration.TreatNoTestsAsError=true
dotnet test -- RunConfiguration.TreatNoTestsAsError=true
```

> "Specify run settings overrides after `--`… Command-line run settings overrides take precedence
> over values from a file passed with `/Settings`. The same syntax works with `dotnet test`."

Source: <https://learn.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file>

NUnit's adapter also has `SkipExecutionWhenNoTests` (bool, default `false`, adapter 4.2.0+), a
*performance* option that skips execution when pre-discovery finds nothing, not an exit-code one.
Related noise: <https://github.com/nunit/nunit3-vs-adapter/issues/929>.

### nunit-console

Documented exit codes: `0` all passed; `1`–`100` number of failures (capped); `-1` `INVALID_ARG`;
`-2` `INVALID_ASSEMBLY` (*"One of the assemblies passed into the console was found to be invalid.
This may include assemblies which contain no tests."*); `-4` `INVALID_TEST_FIXTURE`; `-100`
`UNEXPECTED_ERROR`. There is **no** `NO_TESTS_FOUND` constant and no code path for "the filter
selected nothing", so it should return 0 — **source-derived, not verified empirically**.
Sources: <https://docs.nunit.org/articles/nunit/running-tests/Console-Runner.html>,
<https://github.com/nunit/nunit-console/issues/1628>

### MTP — strict, and this is where it bites

| Exit code | Meaning |
| --- | --- |
| `5` | invalid command-line arguments (the mixed-solution / unrecognised-option failure, §7.5) |
| `8` | "the test session ran zero tests under the strict `--zero-tests-policy`" |
| `9` | "the run executed fewer tests than `--minimum-expected-tests` requires, including zero tests" |
| `15` | `TestExecutionStoppedAtDeadline` — **present in `ExitCodes.cs` on testfx `main` but absent from the Learn table.** Don't treat that table as exhaustive. |

Controls:
- `--zero-tests-policy` — `allow-skipped` (**default**) / `strict`; MTP **2.3.0+**.
- `--minimum-expected-tests` — an explicit value **supersedes** `--zero-tests-policy`, exit 9.
- `--ignore-exit-code <list>` — semicolon-separated (`--ignore-exit-code 2;3;8`); or the
  `TESTINGPLATFORM_EXITCODE_IGNORE` environment variable.

Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>

⚠ **Two Microsoft pages disagree about the default, and the disagreement matters.** The
VSTest-bridge page says: *"MTP will error by default when no tests are discovered or run in a test
application. You can set how many tests you expect to find in the assembly by using
`--minimum-expected-tests` command line parameter, **which defaults to 1**."* The CLI-options page
describes `--zero-tests-policy` as being about *"a run that executes no tests **because every test
was skipped**"* with `allow-skipped` the default. xUnit's own doc and issue tracker side with the
strict reading:

> "If a query filter ends up filtering out all the tests in a test assembly, then Microsoft Testing
> Platform will fail that test assembly since it fails test assemblies without at least 1 test in
> them. You can tell it not return a failure code in this situation by adding
> `--ignore-exit-code 8` to your command line."

Sources: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-extensions-vstest-bridge#runconfiguration-element>,
<https://xunit.net/docs/query-filter-language>, <https://github.com/xunit/xunit/issues/3270>,
<https://github.com/xunit/xunit/issues/3077> ("'Zero test run' from filtered assembly is treated as
error" — closed as not planned)

⚠ **`--minimum-expected-tests 0` does not work** — the validator rejects `0`
(<https://github.com/microsoft/testfx/issues/3121>), and the reporter's own workaround was
`--ignore-exit-code 8`. Whether MTP 2.x accepts `0` is **UNRESOLVED**.

The docs' own tip settles the practical question *for a directly-run executable*:

> "For arguments that every targeted project recognizes, set them directly in
> `TestingPlatformCommandLineArguments` without a condition. For example, all projects recognize
> `--ignore-exit-code 8`."

Source: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>

### 🔴 …but under `dotnet test`, zero-match handling changed between .NET 10 and .NET 11

**Verified directly in `dotnet/sdk` `main`** (I re-checked both files myself rather than relying on
a summary).

On the **.NET 10 SDK**, per-module exit codes are aggregated raw: one project whose filter matches
nothing fails the entire `dotnet test` run. `--ignore-exit-code 8` works, because it is applied
inside each module before aggregation.

On the **.NET 11 SDK**, `TestApplicationActionQueue.cs` normalises it away:

```csharp
// A module that ran zero tests (exit code 8) is not, by itself, a whole-run failure.
// With --test-modules or a global --filter, some modules may legitimately match no tests.
// Normalize it to success here; the aggregate "zero tests ran" verdict is decided once at
// the whole-run level in MicrosoftTestingPlatformTestCommand from the total test count. A
// stricter per-module minimum requested via -- --minimum-expected-tests N still fails that
// module with ExitCode.MinimumExpectedTestsPolicyViolation (9) and is preserved.
// See https://github.com/microsoft/testfx/issues/7457.
result = NormalizeExitCode(result, testApp.HasFailureDuringDispose);
…
internal static int NormalizeExitCode(int result, bool hasFailureDuringDispose)
{
    if (result == ExitCode.ZeroTests) { result = ExitCode.Success; }
    return result == ExitCode.Success && hasFailureDuringDispose ? ExitCode.GenericFailure : result;
}
```

with the run-level verdict in `MicrosoftTestingPlatformTestCommand.cs`:

```csharp
internal static bool ShouldFailForNoExecutedTests(bool isAffectedTestsMode, int totalTests, int skippedTests)
    => (!isAffectedTestsMode && totalTests == 0) ||
       (totalTests > 0 && totalTests == skippedTests);
```

Sources: <https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Test/MTP/TestApplicationActionQueue.cs>,
<https://github.com/microsoft/testfx/issues/7457>

**The SDK team fixed this precisely because test-impact tools create the scenario.** Two
consequences for Reach:

- **`--ignore-exit-code 8` will not save you on .NET 11.** The SDK never inspects that flag;
  suppressing 8 inside each module makes them return 0, and the SDK's own run-level check then
  re-raises 8 if the *total* across all modules is zero. So the escape hatch is
  **SDK-version-dependent**, which is a poor thing to depend on.
- On .NET 11 you only fail if **every** targeted project matched nothing — the sane behaviour for
  impact analysis.

The .NET 11 release notes allude to this — *"…including expected-versus-actual rendering in failure
output, **whole-run zero-test verdict logic**, and automatic post-processing…"* — but the
per-module/run split itself is **source-only and undocumented**.
Source: <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/sdk#dotnet-test-reporter-and-artifacts-improvements>

⚠ Also source-only: `ShouldFailForNoExecutedTests` does **not** honour MTP's
`--zero-tests-policy allow-skipped` — an all-skipped run fails at the SDK level (exit 8) even
though the same executable run directly exits 0. Whether that divergence is intentional is
**UNRESOLVED**.

### Skipping a project outright

| Mode | Mechanism | Notes |
| --- | --- | --- |
| MTP | `dotnet test --project <csproj>` | one project or a directory. Measured ✅ |
| MTP | `dotnet test --solution <sln>` | |
| MTP | `dotnet test --test-modules "<glob>"` | **the best fit for Reach** — globs over already-built DLLs, no build. Measured ✅ running 4 modules via `**/bin/Debug/net10.0/*-mtp.dll`. ⚠ **Single expression only** (repeating the option errors), **no brace expansion**, optional `--root-directory`, mutually exclusive with `--project`/`--solution`, incompatible with `--arch/-c/-f/--os/-r` |
| VSTest | `dotnet test <PROJECT｜SOLUTION｜DIRECTORY｜DLL｜EXE>` | positional; a `.dll`/`.exe` forwards to `vstest`. There is **no** `--project` in VSTest mode |

Making a project a no-op (all **measured**): `IsTestProject=false` (VSTest — skipped, exit 0, no
output), `IsTestingPlatformApplication=false` (MTP), `IsTestApplication=false` (`MSTest.Sdk` helper
libraries). ⚠ **In MTP mode you need *both*** — `SolutionAndProjectUtility.GetModuleFromProject`
skips a project only when `!isTestProject && !isTestingPlatformApplication`. The VSTest skip is
documented only in the tool's own message string in `Microsoft.TestPlatform.targets`: *"Skipping
running test for project {0}. To run tests with dotnet test add
`<IsTestProject>true</IsTestProject>` property to project file."*

And the honest state of the art, from MTP maintainer nohwnd: *"there is currently no official way
to build a solution and then filter the projects to run only selected projects. Closest is a
solution filter file."*
Source: <https://github.com/microsoft/testfx/discussions/7301>
Sources: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp>,
<https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest>,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting>

⚠ **Skipping *everything* is not free.** Measured: in MTP mode, excluding all projects gives
`No test projects were found.` and **exit 1**; a `--test-modules` glob matching nothing gives
`Zero tests ran` and **exit 1**. VSTest mode returns 0.

**Preferred answer for Reach:** don't invoke a runner for a project with an empty selection at all
— emit no command for it, and make the empty-overall-selection case an explicit success in Reach's
own output rather than shelling out. That is strictly better than any flag and works in every host.
Fall back to `--ignore-exit-code 8` only where a single solution-level command is unavoidable.

---

## 9. What could NOT be established

None of these should be guessed at in the spec.

1. **`--treenode-filter` vs parameterised cases.** Whether `/Asm/Ns/Class/Method` matches every
   `[DataRow]` / `[Theory]` case, or whether data rows are child nodes needing
   `/Asm/Ns/Class/Method/**`. Checked: the graph-query-filtering spec (silent), `TreeNodeFilter.cs`
   (parser only — no path construction), `MSTestTestNodeConverter.cs` and `MtpUnitTestElementSink.cs`
   (build `TestNode` Uid/DisplayName; I did not trace where MTP assembles the path a filter
   matches), and `MSTest.Acceptance.IntegrationTests/TestCaseFilteringTests.cs` (covers only
   `FullyQualifiedName=` and `Id=`). GitHub code search was unavailable (403 without auth).
   *Mitigation:* §3.4 already recommends against `--treenode-filter`, so this need not block M1.
2. **Whether `FullyQualifiedName=<method>` really matches all NUnit parameterised cases on adapter
   6.3.** Source says yes (§4.3); issues #677, #607, #919 on older adapters say no. **The highest-
   value empirical check in this document.**
3. **Case sensitivity of `--filter` under the NUnit adapter.** Source suggests case-*sensitive*
   (`ValueMatchFilter` uses `==` and an un-`IgnoreCase` `Regex`), Microsoft's docs say
   case-insensitive. No doc or issue found either way.
4. **Whether `NUnit.Where` reaches the MTP runner.** `--settings config.runsettings` is documented
   for MTP, so `<NUnit><Where>` plausibly arrives, but nothing confirms it, and the inline
   `-- NUnit.Where=` form is a VSTest-path mechanism.
5. **Whether nunit-console returns 0 for a zero-match `--where`.** Source-derived only.
6. **Whether `--testlist` names match parameterised cases.** Source-derived via
   `TestFilterBuilder.AddTest` → `<test>`; not stated in the docs.
7. **MTP ≥ 2.3.0 behaviour when a *filter* (not skipping) selects zero tests**, and whether
   `--minimum-expected-tests 0` is accepted. Two Microsoft pages disagree (§8);
   `microsoft/testfx` `docs/Changelog.md` has no hits for "zero tests" / "zero-tests-policy";
   issue #5762 (MTP v2 breaking changes) doesn't mention it.
8. **Backtick generic arity in `FullyQualifiedName`.** xUnit's `TestClassName` is `Type.FullName`,
   which *would* contain `` `1 `` for an open generic class, but no doc, test, or issue confirms
   what xUnit emits for generic test classes; MSTest builds `{FullClassName}.{Name}` and the
   query-filter doc's own example uses `MyClass+MySubClass` with no backtick. Not constructed and
   tested.
9. **Non-ASCII characters in filter values (VSTest layer).** No rule found in the VSTest filter
   docs, `FilterHelper`, or any adapter source. Absence of a statement is not a guarantee. The
   xUnit hex-reference scheme is the one dialect with a documented answer.
10. **Escapes for `&`, `|`, `*` in the xUnit query filter language.** Not in the doc's list despite
    being grammar operators; `&#x26;` / `&#x7c;` / `&#x2a;` inferred from the general rule plus the
    regex decoder. Also unverified: whether surrogate pairs round-trip through the 1–4-hex-digit
    scheme.
11. **General correctness of `%2C` / `%22` URL-encoding in VSTest filters** beyond the two narrow
    documented cases. The parser source for that decoding was not located.
12. **Latest MSTest / MTP release version numbers.** Not independently pinned; MSTest source claims
    here are against `microsoft/testfx` `main`. The docs pages cited were last updated 2026-08-31 /
    2026-09-02. (The MSTest-specific investigation had not reported when this was written.)
13. **`TestIdGenerationStrategy` runsettings option for MSTest.** Not found in current `testfx`
    source; the current mechanism is the `XxHash128`-based `GenerateSerializedDataStrategyTestId`.
    Whether a legacy knob survives, and whether it can affect `FullyQualifiedName` rather than just
    `Id`, is unresolved — but §4.2's conclusion rests on FQN construction, which is independent.
14. **`vstest.console /Tests:` semantics for parameterised cases.** Docs say it *"matches against
    the full test name, including the namespace"* — for NUnit, whose FQN includes arguments, that is
    ambiguous.
15. **`@file` response-file support for `dotnet test` in VSTest mode**, and whether VSTest mode's
    exit code for "filter matched nothing" differs from "no tests discovered".
16. **Non-Windows and multi-TFM behaviour** of the detection signals in §7.3. The apphost `.exe`
    signal in particular is Windows-specific.
17. **Which VSTest release first shipped the runsettings `<TestCaseFilter>` node.** PR #2356 exists
    and fixes #2273, but the release it landed in was not established. The feature's existence is
    confirmed.
18. **Everything about `--affected-tests` / `--collect-test-map` beyond its existence** (§11) —
    there is no documentation at all, only source.
19. **Whether the .NET 11 SDK's disregard of `--zero-tests-policy allow-skipped` at the run level
    is intentional.** No doc or issue found.

---

## 10. What this means for Reach

1. **Correct the map's "xUnit v4" line** to "xUnit v2 and v3", and key the dialect off framework
   generation (from the `AssemblyRef`, §7.3) plus package version for version-gated options.
2. **Version-gate the xUnit dialect.** MTP `--filter` (VSTest syntax), `--filter-display-name` and
   `-displayName` all need v3 **4.0.0+**. `--filter-query` needs v3. Below 4.0.0 under MTP the
   dialect is `--filter-method` / `--filter-class` / `--filter-query`.
3. **Default renderings** (highest confidence, method-granular, no over-match):
   - MSTest, either host: `--filter "FullyQualifiedName=A|FullyQualifiedName=B|…"`
   - xUnit under VSTest: `--filter "FullyQualifiedName=A|…"` — **never `DisplayName=`**, which
     matches nothing for a theory
   - xUnit v3 under MTP: `--filter-method A B C` (multiple values, one switch)
   - xUnit v3 native CLI: repeated `-method A -method B` (OR within the kind)
   - NUnit: see (4)
4. **Treat NUnit as the correctness hot spot.** The method-level match depends on
   `UseNUnitFilter=true`, `DiscoveryMethod != Legacy`, and the command-line (not IDE) path. Reach
   can't reliably see a consumer's `.runsettings`, and it can't detect the failure — an
   under-selected theory just doesn't run. Options, in preference order:
   - **(a)** Render NUnit selections as `FullyQualifiedName~Ns.C.M` (contains). Correct under
     *both* paths — it matches `Ns.C.M(1,2)` as well as `Ns.C.M`. It over-selects (`MyTest` also
     matches `MyTest2`), and over-selection is explicitly safe. Note `~` becomes an **unanchored
     regex** on the NUnit path, with metacharacters escaped by the adapter — and note the
     performance cliff in issue #1084 before using many `~` clauses.
   - **(b)** Emit both `FullyQualifiedName=Ns.C.M|FullyQualifiedName~Ns.C.M(` — narrower
     over-selection, but doubles filter length and needs `(` escaped in two layers.
   - **(c)** Fall back to **whole-project selection** for NUnit in M1 and revisit in M2 once a
     fixture pins the behaviour.
   Under the map's rule — "where a decision is genuinely uncertain, take the option that widens the
   selection" — **(a)** is the right default, recorded as a deliberate over-selection with a
   fixture that pins it. Also: emit exact-case names (§3.3), keep clause counts **well under
   2000** (`AssemblySelectLimit`), and prefer adapter ≥ 6.2.0 when MTP is in play.
5. **Prefer per-project invocation over a solution-level `dotnet test`.** It avoids the
   exit-code-5 mixed-solution failure entirely, avoids writing `Directory.Build.props` into the
   user's repo, gives a clean escaping story, and makes "skip a project with an empty selection"
   trivial. This should shape the report contract (issue 07) and the CLI surface (issue 08).
6. **Budget for length, and pick the right escape hatch per host.** 8,191 characters if a command
   may pass through `cmd`, 32,767 otherwise. `.runsettings` `<TestCaseFilter>` has *no* limit, is
   maintainer-tested to ~3.5 MB, and is the best VSTest answer — but **detect a pre-existing
   `<TestCaseFilter>`, which AND-s with yours**. `--testlist=FILE` is the best nunit-console
   answer; MTP takes `@file.rsp` (token-per-line via `dotnet test`); xUnit's native CLI takes
   `@@ file` as its *only* arguments. `dotnet test` in VSTest mode has no working response file.
   Chunking into multiple invocations is the portable fallback.
7. **Do not rely on `--ignore-exit-code 8`.** It works for a directly-run MTP executable and for
   `dotnet test` on the .NET 10 SDK, but **not** for `dotnet test` on .NET 11, where the run-level
   verdict is computed after per-module codes are normalised (§8). Emitting no command for an
   empty selection is the only host- and version-independent answer.
8. **Runner-host detection needs `global.json` resolved from the invocation CWD**, plus the SDK
   version — not just the assembly and project file. Report per-project *capabilities*
   (VSTest-capable / MTP-capable, possibly both) and resolve the actual host separately. Where
   MSBuild evaluation is available (`dotnet msbuild -getProperty:IsTestProject,IsTestingPlatformApplication`),
   prefer it — it is what the SDK itself uses. Note this conflicts with the map's "assembly
   discovery scans output directories, not MSBuild" decision, so the fallback chain in §7.3
   matters.
9. **Multiple frameworks in one project is legal.** Treat framework as a set and fall back to
   **whole-project selection** when it isn't a singleton.
10. **Investigate `--affected-tests` before finalising the architecture** (§11).
11. **Fixtures worth committing (issue 11)** — every one of these is a place where a doc could be
   wrong or drift:
   - A two-row `[Theory]` / `[TestCase]` / `[TestCaseSource]` / `[DataRow]` in each framework,
     selected by a method-level filter, asserting **both rows ran**. Run it under every host the
     framework supports.
   - The NUnit case specifically on adapter 6.3 with default settings (unresolved item 2).
   - A mixed-case filter value against NUnit (unresolved item 3).
   - Method names needing escaping in each layer: `(`, `,`, non-ASCII, generic arity, a nested
     type, an overloaded method.
   - A selection large enough to cross NUnit's 2000-clause `AssemblySelectLimit`.
   - An empty selection, asserting exit code 0 in every host.
   - A theory whose display name exceeds 447 characters (guards against anyone ever switching the
     xUnit dialect to `DisplayName=`).

---

## 11. 🔴 The .NET SDK is building test impact analysis

Found while investigating zero-test handling, and **verified directly against `dotnet/sdk` `main`**
rather than taken on trust. `TestCommandDefinition.MicrosoftTestingPlatform.cs`:

```csharp
public const string EnableAffectedTestsEnvironmentVariable = "DOTNET_CLI_ENABLE_AFFECTED_TESTS";
public const string CollectTestMapOptionName = "--collect-test-map";
public const string AffectedTestsOptionName  = "--affected-tests";
…
AffectedTestsEnabled = EnvironmentVariableParser.ParseBool(
    Environment.GetEnvironmentVariable(EnableAffectedTestsEnvironmentVariable), defaultValue: false);

CollectTestMapOption = new(CollectTestMapOptionName)
{ Description = CommandDefinitionStrings.CmdCollectTestMapDescription, Arity = ArgumentArity.Zero, Hidden = !AffectedTestsEnabled };

AffectedTestsOption = new(AffectedTestsOptionName)
{ Description = CommandDefinitionStrings.CmdAffectedTestsDescription,  Arity = ArgumentArity.Zero, Hidden = !AffectedTestsEnabled };
```

Both options are registered unconditionally and merely **hidden** unless
`DOTNET_CLI_ENABLE_AFFECTED_TESTS` is set; a validator rejects them when the flag is off. And the
run-level zero-test verdict has a dedicated branch for the mode —
`ShouldFailForNoExecutedTests(isAffectedTestsMode: true, 0, 0) == false`, i.e. **an affected-tests
run that selects nothing is deliberately allowed to succeed** (§8).

Source: <https://github.com/dotnet/sdk/blob/main/src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Test/TestCommandDefinition.MicrosoftTestingPlatform.cs>

**There is no documentation for either option** — no Learn page, no release-note entry. Everything
above is source-only, and the shape of the feature (what `--collect-test-map` collects, what
granularity it selects at, whether it walks a call graph or records coverage at runtime) is
**UNRESOLVED**.

> **Why this matters to Reach, and what it does not mean.** It does not make Reach redundant: the
> feature is unshipped, undocumented, MTP-only, and its approach is unknown — a runtime test-map
> collection is a fundamentally different technique from Reach's static reverse reachability over
> compiled assemblies, with different blind spots. But it is directly adjacent, it is being built
> by the team that owns `dotnet test`, and it will shape what a Reach user's pipeline looks like in
> a year.
>
> **Recommended next step:** a short spike — read the `--collect-test-map` implementation and
> testfx#7457's surrounding work, and find out whether the test map is a format Reach could emit
> or consume. Worth an issue of its own before the M1 spec is frozen; it bears on the report
> contract (issue 07) and the CLI surface (issue 08). This is a strategic question, not a
> walking-skeleton blocker.
