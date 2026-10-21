using System.Xml;
using System.Xml.Linq;
using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads package items from MSBuild XML (a .csproj or Directory.Packages.props) as text: no evaluation, no imports, no restore, and a DTD is refused.</summary>
internal static class MsBuildItems
{
    private const string DevelopmentKind = "dev";
    private const string RuntimeKind = "runtime";

    internal static IReadOnlyList<DepRecord> Read(string manifest, string text, IReadOnlyDictionary<string, string> kindByItem)
    {
        var root = Load(text);
        var set = new DepRowSet();
        if (root is null)
        {
            return set.Rows;
        }

        foreach (var item in root.Descendants())
        {
            if (set.Full)
            {
                break;
            }

            if (kindByItem.TryGetValue(item.Name.LocalName, out var kind))
            {
                set.Add(Row(manifest, item, kind));
            }
        }

        return set.Rows;
    }

    private static DepRecord Row(string manifest, XElement item, string kind)
    {
        var version = Value(item, "VersionOverride") ?? Value(item, "Version") ?? string.Empty;
        var isDevelopment = kind == RuntimeKind && string.Equals(Value(item, "PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase);
        var conditional = item.AncestorsAndSelf().Any(element => element.Attribute("Condition") is not null);
        return new DepRecord(manifest, Value(item, "Include") ?? string.Empty, version.Trim(), isDevelopment ? DevelopmentKind : kind, conditional ? "conditional" : string.Empty);
    }

    // An attribute, or a child element of the same name (both spellings are valid MSBuild).
    private static string? Value(XElement item, string name)
        => item.Attribute(name)?.Value ?? item.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;

    private static XElement? Load(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            return XDocument.Load(reader).Root;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
