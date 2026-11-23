using Reach.Tests.Acceptance;

namespace Reach.Tests.Harness;

/// <summary>
/// The known-gap guard (spec §4.5): a test named <c>Known_gap_…</c> carries the
/// <c>Batch=known-gap</c> trait, and a test with the trait is named <c>Known_gap_…</c>. Outside
/// the acceptance namespace, so it runs in the fast CI step.
/// </summary>
public sealed class KnownGapGuardTests
{
    [Fact]
    public void Every_known_gap_test_in_the_suite_is_both_named_and_traited()
    {
        var failures = KnownGapGuard.Failures(KnownGapGuard.TestMethodsOf(typeof(KnownGapGuardTests).Assembly));

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void The_suite_has_known_gap_tests_for_the_guard_to_check()
    {
        var methods = KnownGapGuard.TestMethodsOf(typeof(KnownGapGuardTests).Assembly);

        Assert.Contains(methods, method => method.Name.StartsWith(KnownGapGuard.Prefix, StringComparison.Ordinal));
    }

    [Fact]
    public void A_named_and_traited_method_passes()
    {
        Assert.Empty(KnownGapGuard.Failures([Method(nameof(Matched.Known_gap_named_and_traited))]));
    }

    [Fact]
    public void A_method_with_neither_the_name_nor_the_trait_passes()
    {
        Assert.Empty(KnownGapGuard.Failures([Method(nameof(Matched.Neither))]));
    }

    [Fact]
    public void Each_mismatched_method_is_listed()
    {
        var failures = KnownGapGuard.Failures(
        [
            Method(nameof(Mismatched.Known_gap_without_the_trait)),
            Method(nameof(Mismatched.Traited_without_the_name)),
        ]);

        Assert.Equal(
            [
                $"{typeof(Mismatched).FullName}.Known_gap_without_the_trait is named Known_gap_ but lacks [KnownGap].",
                $"{typeof(Mismatched).FullName}.Traited_without_the_name has [KnownGap] but is not named Known_gap_.",
            ],
            failures);
    }

    [Fact]
    public void A_plain_trait_with_the_same_name_and_value_counts_as_the_trait()
    {
        Assert.Empty(KnownGapGuard.Failures([Method(nameof(Matched.Known_gap_with_a_plain_trait))]));
    }

    [Fact]
    public void The_suite_scan_sees_only_test_methods()
    {
        var methods = KnownGapGuard.TestMethodsOf(typeof(KnownGapGuardTests).Assembly);

        Assert.DoesNotContain(methods, method => method.DeclaringType == typeof(Mismatched));
        Assert.Contains(methods, method => method.Name == nameof(Each_mismatched_method_is_listed));
    }

    private static System.Reflection.MethodInfo Method(string name) =>
        typeof(Matched).GetMethod(name) ?? typeof(Mismatched).GetMethod(name)
            ?? throw new InvalidOperationException($"No fixture method {name}.");

    /// <summary>
    /// Methods the guard accepts. Not test methods, so the suite scan never sees them.
    /// </summary>
    internal static class Matched
    {
        [KnownGap]
        public static void Known_gap_named_and_traited()
        {
            // Only its name and attributes matter: the guard reads them by reflection.
        }

        [Trait("Batch", "known-gap")]
        public static void Known_gap_with_a_plain_trait()
        {
            // Only its name and attributes matter: the guard reads them by reflection.
        }

        public static void Neither()
        {
            // Only its name and attributes matter: the guard reads them by reflection.
        }
    }

    /// <summary>
    /// Methods built for the guard to reject. Not test methods, so the suite scan never sees them.
    /// </summary>
    internal static class Mismatched
    {
        public static void Known_gap_without_the_trait()
        {
            // Only its name and attributes matter: the guard reads them by reflection.
        }

        [KnownGap]
        public static void Traited_without_the_name()
        {
            // Only its name and attributes matter: the guard reads them by reflection.
        }
    }
}
