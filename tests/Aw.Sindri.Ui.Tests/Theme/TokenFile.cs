using System.Globalization;
using System.Xml.Linq;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>The resources Theme/Tokens.axaml declares, read as XML (no Avalonia needed, so the unit lane can use them).</summary>
internal sealed class TokenFile
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private TokenFile(XDocument document)
    {
        Document = document;
    }

    /// <summary>The parsed file.</summary>
    internal XDocument Document { get; }

    /// <summary>Theme/Tokens.axaml.</summary>
    internal static TokenFile Load() => new(XDocument.Load(Path.Combine(UiSource.Theme, "Tokens.axaml")));

    /// <summary>Every resource element with its x:Key.</summary>
    internal IReadOnlyDictionary<string, XElement> Resources =>
        Document.Descendants().Where(e => e.Attribute(_x + "Key") is not null).ToDictionary(e => e.Attribute(_x + "Key")!.Value, StringComparer.Ordinal); // why: filtered on the line above

    /// <summary>The colour of Color.&lt;name&gt; as #rrggbb (alpha dropped when opaque).</summary>
    internal string Color(string name) => Resources["Color." + name].Value.Trim().ToLowerInvariant();

    /// <summary>The relative luminance of Color.&lt;name&gt; (WCAG 2.2).</summary>
    internal double Luminance(string name) => Wcag.Luminance(Color(name));

    /// <summary>The contrast ratio of two tokens (WCAG 2.2).</summary>
    internal double Contrast(string foreground, string background) => Wcag.Contrast(Color(foreground), Color(background));

    /// <summary>A Double resource.</summary>
    internal double Number(string key) => double.Parse(Resources[key].Value.Trim(), CultureInfo.InvariantCulture);
}
