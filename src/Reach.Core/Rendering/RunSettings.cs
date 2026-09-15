using System.Xml.Linq;

namespace Reach.Rendering;

/// <summary>
/// The <c>--runsettings</c> opt-in.
/// </summary>
/// <remarks>
/// <para>
/// <c>.runsettings</c> is <strong>not</strong> the default channel despite having no length
/// limit at all. Two independent disqualifiers: a pre-existing <c>&lt;TestCaseFilter&gt;</c> is
/// <em>AND-ed</em> with Reach's, and a consumer's filter is typically an exclusion, so the
/// intersection is strictly smaller than the selection — an undetectable silent under-selection;
/// and only one settings file can be passed at all, so a file Reach writes <em>replaces</em> the
/// consumer's. Reach is invoked as a separate step that never sees the runner's arguments, so
/// detection cannot rescue either.
/// </para>
/// <para>
/// Passing <c>--runsettings</c> supplies the fact Reach could not discover, which is what makes
/// merging legitimate.
/// </para>
/// </remarks>
internal static class RunSettings
{
    private const string FilterElement = "TestCaseFilter";

    /// <summary>
    /// Whether the caller's file already carries a filter. If it does, Reach refuses to render
    /// one for that project and downgrades it to <c>run-all</c> — the intersection would be
    /// silently smaller than the selection.
    /// </summary>
    internal static bool CarriesAFilter(string path)
    {
        try
        {
            return XDocument.Load(path).Descendants()
                .Any(element => element.Name.LocalName == FilterElement
                    && !string.IsNullOrWhiteSpace(element.Value));
        }
        catch (System.Xml.XmlException)
        {
            // Unreadable: treat it as carrying one, which downgrades to run-all. The widening
            // direction, and the alternative is writing a merged file over a file Reach could
            // not read.
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>Writes the caller's settings with Reach's filter merged in, and returns the path.</summary>
    internal static string Merge(string source, string filter, string destination)
    {
        var document = Load(source);
        var root = document.Root ?? new XElement("RunSettings");

        var runConfiguration = root.Elements().FirstOrDefault(e => e.Name.LocalName == "RunConfiguration");

        if (runConfiguration is null)
        {
            runConfiguration = new XElement("RunConfiguration");
            root.Add(runConfiguration);
        }

        runConfiguration.Add(new XElement(FilterElement, filter));

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        new XDocument(root).Save(destination);

        return destination;
    }

    private static XDocument Load(string path)
    {
        try
        {
            return XDocument.Load(path);
        }
        catch (System.Xml.XmlException)
        {
            return new XDocument(new XElement("RunSettings"));
        }
        catch (IOException)
        {
            return new XDocument(new XElement("RunSettings"));
        }
    }
}
