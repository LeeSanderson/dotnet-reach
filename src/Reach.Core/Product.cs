namespace Reach;

/// <summary>Facts about the running tool, for the report envelope and <c>--version</c>.</summary>
internal static class Product
{
    /// <summary>The informational version of the assembly Reach is running from.</summary>
    internal static string Version { get; } =
        typeof(Product).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
            ?.Split('+')[0]
        ?? "0.0.0";
}
