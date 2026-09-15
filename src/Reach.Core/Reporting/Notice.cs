namespace Reach.Reporting;

/// <summary>
/// One thing a run has to disclose. Identified by a stable kebab-case code that is never
/// renamed, so it can be documented once and depended on.
/// </summary>
/// <param name="Code">From <see cref="NoticeCodes"/>. Never renamed, never numeric.</param>
/// <param name="Kind">The one axis. There is deliberately no severity alongside it.</param>
/// <param name="Message">
/// Complete on its own, so a consumer ignoring <paramref name="Data"/> loses structure but
/// never meaning.
/// </param>
/// <param name="Data">
/// Free-form per code. The names <c>projects</c>, <c>assemblies</c>, <c>paths</c> and
/// <c>members</c> are reserved and used consistently wherever they apply.
/// </param>
internal sealed record Notice(
    string Code,
    NoticeKind Kind,
    string Message,
    IReadOnlyDictionary<string, object?>? Data = null)
{
    internal static Notice Environment(string code, string message, params (string Key, object? Value)[] data) =>
        new(code, NoticeKind.Environment, message, ToData(data));

    private static IReadOnlyDictionary<string, object?>? ToData((string Key, object? Value)[] data) =>
        data.Length == 0
            ? null
            : data.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
}
