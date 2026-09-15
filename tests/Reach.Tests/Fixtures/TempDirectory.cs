namespace Reach.Tests.Fixtures;

/// <summary>
/// A real directory under the system temp root, deleted on dispose. There is no filesystem
/// abstraction in Reach — a real temporary directory is less work than mock filesystem setup,
/// and every filesystem test here needs a real git repository anyway.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path) => Path = path;

    internal string Path { get; }

    /// <summary>Creates a directory whose leaf name starts with <paramref name="name"/>.</summary>
    internal static TempDirectory Create(string name = "reach")
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{name}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    internal string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Writes a file, creating any intermediate directories.</summary>
    internal string WriteFile(string relativePath, string contents)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A git pack file or a killed child can still hold a handle. Leaking a temp
            // directory is not worth failing a green test over.
        }
        catch (UnauthorizedAccessException)
        {
            // Same, for read-only files git writes under .git/objects.
        }
    }
}
