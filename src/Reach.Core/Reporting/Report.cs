using System.Text.Json.Serialization;

namespace Reach.Reporting;

/// <summary>
/// The machine-readable record of one run: the canonical selection, why each test was selected,
/// what could not be analysed, and the run's own inputs.
/// </summary>
/// <remarks>
/// <para>
/// A product surface with a versioned schema, not a log — a selection nobody can audit gets
/// switched off. Property order here is the serialised order, and every array is sorted by a
/// documented key, because the report is byte-deterministic for a given input.
/// </para>
/// <para>
/// <strong><c>schemaVersion</c> is a single integer, and the consumer's obligation is part of
/// the contract</strong>: tolerate unknown fields, unknown enum members and unknown notice
/// codes. That tolerance is what makes new codes, kinds and outcomes additive. Breaking means
/// removing a field, repurposing one, or changing its type. M1 emits version 1 and cannot emit
/// any other.
/// </para>
/// </remarks>
internal sealed record Report
{
    internal const int Version = 1;

    public int SchemaVersion { get; init; } = Version;

    public required string Outcome { get; init; }

    /// <summary>
    /// Notice codes, never prose, and required non-empty when the outcome is
    /// <c>nothing-selected</c> — so every cause of emptiness is a documented, register-linked
    /// code and the taxonomy is enumerable rather than a place people add ad-hoc strings.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Reasons { get; init; }

    public required ReportEnvelope Envelope { get; init; }

    public required ReportScope Scope { get; init; }

    /// <summary>The over-selection measurement. Arithmetic over inputs already carried.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReportMeasurement? Summary { get; init; }

    /// <summary>
    /// Every change, with its tier and the count of tests it reached. Counts only, never test
    /// names — the pairs live once, on the test side.
    /// </summary>
    public required IReadOnlyList<ReportChange> Changes { get; init; }

    /// <summary>One per (test project, target framework), uniformly, even for a single-targeted project.</summary>
    public required IReadOnlyList<ReportEntry> Entries { get; init; }

    /// <summary>A single global array with locators, never notices on entries as well.</summary>
    public required IReadOnlyList<ReportNotice> Notices { get; init; }
}

/// <summary>
/// The run's own inputs. Everything needed to reproduce it, and nothing that would make two
/// runs over the same input differ.
/// </summary>
internal sealed record ReportEnvelope
{
    public required string ToolVersion { get; init; }

    /// <summary>
    /// Resolved to a SHA, not left as <c>origin/main</c>. It earns its place alone: the
    /// empty-change-set trap <em>is</em> a baseline that quietly resolved to <c>HEAD</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReportBaseline? Baseline { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Head { get; init; }

    /// <summary>Which of committed, staged, unstaged and untracked were read.</summary>
    public required IReadOnlyList<string> ChangeSources { get; init; }

    public required string BuildMode { get; init; }

    /// <summary>Everything after <c>--</c>, verbatim, so a surprising run stays reproducible.</summary>
    public required IReadOnlyList<string> ForwardedBuildArguments { get; init; }

    public required string Correspondence { get; init; }

    /// <summary>
    /// Segregated into this one clearly-marked object, so that excluding a single key makes two
    /// reports comparable. No test durations — Reach does not run tests.
    /// </summary>
    public required ReportTimings Timings { get; init; }
}

internal sealed record ReportBaseline(string Sha, string Reference, string DetectedFrom, bool IsHead);

internal sealed record ReportTimings
{
    public required string StartedUtc { get; init; }

    public required long TotalMs { get; init; }

    /// <summary>Reach's own phases, plus the build when Reach ran it.</summary>
    public required IReadOnlyDictionary<string, long> Phases { get; init; }
}

/// <summary>
/// Test projects always, and in-scope assemblies as a flat list of identities. <strong>Never
/// closure edges</strong> — those are the project graph and belong nowhere near a report.
/// </summary>
internal sealed record ReportScope
{
    public required IReadOnlyList<string> TestProjects { get; init; }

    public required IReadOnlyList<ReportAssembly> Assemblies { get; init; }
}

internal sealed record ReportAssembly(string Name, string TargetFramework);

/// <param name="Tier">Which granularity the change was routed at.</param>
/// <param name="TestsReached">
/// The field that makes an under-selection visible: a count of zero here is either a genuine
/// coverage gap or an under-selection bug, and today both are invisible.
/// </param>
internal sealed record ReportChange(
    int Index,
    string Display,
    string Tier,
    string Reason,
    int TestsReached);

internal sealed record ReportEntry
{
    public required string Project { get; init; }

    public required string TargetFramework { get; init; }

    /// <summary>All four are needed: the package version changed xUnit's filter surface.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PackageVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunnerHost { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dialect { get; init; }

    public required string Mode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Delivery { get; init; }

    public required ReportCounts Counts { get; init; }

    public required IReadOnlyList<ReportTest> Tests { get; init; }

    /// <summary>
    /// Always an array: many when chunked, one for <c>run-all</c>, <strong>zero for
    /// <c>skip</c></strong>. That is the load-bearing part — a consumer iterating blindly does
    /// nothing for an empty selection, so "emit no command at all" is the only representable
    /// answer rather than a rule to remember. Argv vectors, never shell strings.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<string>> Invocations { get; init; }
}

/// <param name="Total">
/// Null serialises as <c>unknown</c>. Reporting zero for a project Reach could not enumerate
/// would silently corrupt the over-selection ratio in exactly the case where Reach is running
/// an entire project.
/// </param>
internal sealed record ReportCounts(int Selected, int WillRun, int? Total)
{
    [JsonPropertyName("total")]
    public object TotalOrUnknown => Total.HasValue ? Total.Value : "unknown";

    [JsonIgnore]
    public int? Total { get; init; } = Total;
}

internal sealed record ReportTest
{
    public required string Display { get; init; }

    public required string Id { get; init; }

    public required IReadOnlyList<string> Rules { get; init; }

    /// <summary>Indices into <see cref="Report.Changes"/>. Empty exactly when not reverse-reachable.</summary>
    public required IReadOnlyList<int> Changes { get; init; }

    /// <summary>Null exactly when not reverse-reachable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PathClass { get; init; }

    /// <summary>Hop-by-hop, only under <c>--paths</c>. Every hop names a kernel method.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<IReadOnlyList<string>>? Paths { get; init; }
}

internal sealed record ReportNotice
{
    public required string Code { get; init; }

    public required string Kind { get; init; }

    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
