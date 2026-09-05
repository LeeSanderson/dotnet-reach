# What is `dotnet test --affected-tests`?

Type: research
Status: resolved
Blocked by: (none)

## Question

[Filter dialects and runner detection](01-filter-dialects-and-runner-detection.md) found,
directly in `dotnet/sdk` source, that the .NET 11 SDK carries two hidden,
environment-variable-gated options: **`dotnet test --affected-tests`** and
**`--collect-test-map`**. They are documented nowhere.

The names describe the problem Reach exists to solve. That does not make Reach redundant —
`--collect-test-map` reads like a *coverage-based* technique, which PRD §9.1 rejected for
requiring standing infrastructure, and coverage and static analysis have different blind
spots — but it is close enough that the M1 spec should not be frozen without knowing what
it is.

**Establish:**

- What the two options actually do, from the SDK source: what a "test map" contains, when
  it is collected, where it is stored, and what `--affected-tests` consults to decide.
- Which technique: per-test coverage instrumentation, project-graph reachability, or
  something else. This is the decisive question, because it determines whether the two
  tools are alternatives or complements.
- What state it requires between runs, and where that state lives. ADR-0001 makes Reach
  stateless; if this feature needs a stored index keyed by commit, it inherits exactly the
  infrastructure cost PRD §9.1 rejected, and Reach's positioning is unchanged.
- Which environment variable gates it, what shipping vehicle and timeline it is on, and
  whether there is any public design discussion, issue or PR explaining intent.
- Whether it is tied to Microsoft.Testing.Platform only, or works under VSTest too, and
  which test frameworks it supports.

**Then answer the product question honestly:** does this change what Reach should be? The
possible outcomes are that it is complementary and Reach proceeds unchanged; that it
overlaps enough to change Reach's positioning or scope; or that it makes M1 as specified a
poor use of two weeks. All three are acceptable answers and the third must not be softened
if it is the true one.

Research only — no code, no git operations. Findings go to
`.scratch/walking-skeleton/research/`, with the product judgement stated plainly and
separately from the established facts.

## Answer

Full findings: [research/16-what-is-dotnet-test-affected-tests.md](../research/16-what-is-dotnet-test-affected-tests.md).

**It is coverage-based runtime instrumentation.** Three independent primary sources agree.
The design issue, `dotnet/sdk` #55405, states the workflow verbatim: *"Execute tests and
record which source files and binaries are exercised by each test. Persist that mapping as
a reusable source of truth. Compare the current Git changes with the stored mapping."*
That is PRD §3's definition of coverage-based analysis nearly word for word. testfx RFC 020
describes a profiler-based collector; testfx's `global.json` carries a
`test.affectedTests.instrumentation.include/exclude` scope. It is not project-graph
reachability and not static analysis of any kind.

**It requires exactly the infrastructure PRD §9.1 rejected.** Microsoft's own pipeline is a
trusted main-branch collection lane forced to run serially, an Azure Pipelines `Cache@2`
store keyed by cache version, OS, architecture and configuration, a seven-day expiry where
*"a cache miss is therefore an expected state"*, and a permanent full-suite fallback leg.
Map keys must also account for target framework, runtime and device.

**The SDK contains none of the logic** — two zero-arity flags, some validation, and one
environment variable set on the child process. PR #55574: *"Repository analysis, test-map
collection/storage, and affected-test filtering remain in a separately distributed MTP
extension."* That extension is private and unpublished; no such package exists on NuGet,
and testfx's own rollout is disabled because *"the affected-test extension package and its
public local-filesystem storage contract are also not available yet."*

**Correction to the finding that triggered this ticket:** these options are **not** in a
shipping SDK. The newest public .NET 11 is preview.7 and its branch does not contain them;
they exist only in `main` and the `release/11.0.1xx` branches. A scan of the local SDK
10.0.303 found zero hits, with the scan validated against known-present strings.

Also established: gated by `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1`; MTP-only by explicit
decision, with *"VSTest support is out of scope"*; selection delivered as MTP test-node
UIDs rather than filter strings; zero documentation; no activity in either repository since
2026-08-05.

### Product judgement: complementary. M1 proceeds unchanged.

The .NET team independently converged on the approach PRD §9.1 rejected and paid its costs
in full, so §9.1's reasoning now has a worked example authored by the people building the
alternative. The feature is in no public SDK, undocumented, and its engine is an unpublished
package that currently blocks Microsoft's own rollout.

**Two things are honestly worse than the PRD assumes**, and it should say so rather than
absorb them quietly. Coverage is more precise exactly where PRD §11 says Reach is weakest —
over-selection through interface dispatch in DI-heavy codebases, which a coverage map does
not have. And "no infrastructure ask" is a narrower moat than assumed: once the extension
is public, adoption on Azure DevOps is a package, a `global.json` block and a cache task.
The moat holds best for the secondary consultant audience, for non-Azure CI, and for the
non-MTP majority.

**Do not design for map interop.** There is no published format and Microsoft is explicitly
declining to publish one yet.

### Not established

The map's on-disk format, schema and granularity (explicitly withheld). Whether collection
is incremental or always a full instrumented run. Which profiler — no primary source links
`Microsoft.Testing.Extensions.CodeCoverage` to this extension. The extension's supported
frameworks, package id, licence, acquisition channel, or whether it will ever be public.
How stale or incompatible maps are detected. Whether .NET 11 GA ships these options at all.
