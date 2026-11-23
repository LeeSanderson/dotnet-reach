using System.Globalization;

namespace Reach.Tests.Acceptance;

/// <summary>
/// The lines that differ between two texts, by longest common subsequence. Enough to show where
/// two reports part, which is all a determinism failure needs.
/// </summary>
internal static class LineDiff
{
    /// <summary>
    /// <c>- n: line</c> for a line only <paramref name="before"/> has, and <c>+ n: line</c> for one
    /// only <paramref name="after"/> has, each numbered in its own text. Lines both share are
    /// left out.
    /// </summary>
    internal static IReadOnlyList<string> Of(string before, string after)
    {
        var left = before.Split('\n');
        var right = after.Split('\n');
        var common = CommonSuffixLengths(left, right);
        var differences = new List<string>();

        var (i, j) = (0, 0);

        while (i < left.Length || j < right.Length)
        {
            if (i < left.Length && j < right.Length && left[i] == right[j])
            {
                (i, j) = (i + 1, j + 1);
            }
            else if (j == right.Length || (i < left.Length && common[i + 1][j] >= common[i][j + 1]))
            {
                differences.Add(Line('-', i, left[i]));
                i++;
            }
            else
            {
                differences.Add(Line('+', j, right[j]));
                j++;
            }
        }

        return differences;
    }

    /// <summary>
    /// <c>[i][j]</c> is the length of the longest common subsequence of the lines from
    /// <c>i</c> and <c>j</c> onwards.
    /// </summary>
    private static int[][] CommonSuffixLengths(string[] left, string[] right)
    {
        var lengths = new int[left.Length + 1][];

        for (var i = 0; i <= left.Length; i++)
        {
            lengths[i] = new int[right.Length + 1];
        }

        for (var i = left.Length - 1; i >= 0; i--)
        {
            for (var j = right.Length - 1; j >= 0; j--)
            {
                lengths[i][j] = left[i] == right[j]
                    ? lengths[i + 1][j + 1] + 1
                    : Math.Max(lengths[i + 1][j], lengths[i][j + 1]);
            }
        }

        return lengths;
    }

    private static string Line(char sign, int index, string text) =>
        string.Create(CultureInfo.InvariantCulture, $"{sign} {index + 1}: {text}");
}
