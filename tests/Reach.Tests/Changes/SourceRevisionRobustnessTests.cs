using Reach.Changes;

namespace Reach.Tests.Changes;

/// <summary>
/// Reach parses whatever git hands it, which is not always a file that compiles — a truncated
/// blob, a conflict marker, a generated stub. Nothing here may throw.
/// </summary>
public class SourceRevisionRobustnessTests
{
    [Fact]
    public void Every_source_file_in_this_repository_parses_without_throwing()
    {
        var root = RepositoryRoot();
        var failures = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                SourceRevision.DeclaredTypes(File.ReadAllText(file));
            }
            catch (Exception thrown)
            {
                failures.Add($"{Path.GetRelativePath(root, file)}: {thrown.GetType().Name}: {thrown.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("﻿namespace N; public class C { public int M() => 1; }")]
    [InlineData("namespace N; public class C { public int M() => ")]
    [InlineData("<<<<<<< HEAD\nnamespace N;\n=======\nnamespace M;\n>>>>>>> other\n")]
    [InlineData("namespace N; public class C { public int M() => 1; } // no trailing newline")]
    public void Source_that_does_not_compile_is_still_read_without_throwing(string source) =>
        SourceRevision.DeclaredTypes(source);

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null && !Directory.Exists(Path.Combine(directory, ".git")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return directory;
    }
}
