namespace Reach.Projects;

/// <summary>
/// One project file as Reach reads it: enough to build the project graph and to know what
/// assembly instances it is expected to produce, and nothing more. MSBuild is never invoked.
/// </summary>
/// <param name="Path">Absolute and full — the key a project is identified by.</param>
/// <param name="AssemblyName">The <c>AssemblyName</c> property, defaulting to the file's base name.</param>
/// <param name="TargetFrameworks">
/// One entry for a single-targeted project, several for a multi-targeted one. Each pairs with
/// the project to make an expected assembly instance.
/// </param>
/// <param name="References">
/// <c>ProjectReference</c> elements, read from raw XML rather than from assembly references in
/// metadata: the compiler omits references to assemblies whose types are never named, so the
/// metadata closure is narrower than the real one.
/// </param>
/// <param name="PackageReferences">Package ids only. What test-framework recognition reads.</param>
internal sealed record ProjectFile(
    string Path,
    string AssemblyName,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ProjectReference> References,
    IReadOnlyList<string> PackageReferences)
{
    /// <summary>The file's base name, which is how a project is named in messages.</summary>
    internal string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    internal string Directory => System.IO.Path.GetDirectoryName(Path)!;
}

/// <summary>A <c>ProjectReference</c>, with the two attributes that change what it means.</summary>
/// <param name="Path">Absolute and full, resolved against the referencing project's directory.</param>
/// <param name="ReferenceOutputAssembly">
/// <c>false</c> produces neither a metadata reference nor a copy — <c>Private</c> is the
/// separate switch governing copying. Such a reference expresses build order rather than
/// runtime executability, so it stays in the closure (which errs wide, the safe direction)
/// but contributes no expected assembly instance.
/// </param>
/// <param name="IsAnalyzer">
/// <c>OutputItemType="Analyzer"</c> — a source generator or analyser, whose output is loaded
/// by the compiler and never lands beside the consumer's own assemblies.
/// </param>
internal sealed record ProjectReference(string Path, bool ReferenceOutputAssembly, bool IsAnalyzer);
