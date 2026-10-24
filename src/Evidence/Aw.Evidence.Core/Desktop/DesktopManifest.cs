using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>
/// The repo's screens manifest (schema 'ui-evidence.desktop-screens/1'): every screen of the desktop app that has a design, with the route the
/// app opens it by, the HTML mockup to compare it with and what may differ. Unknown keys are refused.
/// </summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="DesignRoot">The folder of the mockups, relative to the manifest's folder.</param>
/// <param name="Screens">The screens, in the order they are reported.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent; the config's when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopManifest(string Schema, string DesignRoot, IReadOnlyList<DesktopScreen> Screens, double? Threshold = null)
{
    /// <summary>The schema this build reads.</summary>
    public const string SchemaName = "ui-evidence.desktop-screens/1";
}

/// <summary>One screen: how the app opens it, which mockup is its design, the data it needs and what may differ.</summary>
/// <param name="Name">Lowercase letters, digits and '-'; names the screen's files.</param>
/// <param name="Route">What the capture host opens: 'page:&lt;Page&gt;' or a deep link of the app ('item/ITM-01JE7').</param>
/// <param name="Design">The mockup's HTML file, relative to the design root.</param>
/// <param name="Fixture">The fixture steps this screen needs beyond the config's 'up'; each is the arguments of one step.</param>
/// <param name="Threshold">This screen's limit of differing pixels, in percent; the manifest's when null.</param>
/// <param name="Masks">Regions painted over in both frames before they are compared (live data, clocks, ids).</param>
/// <param name="Accepted">Differences the owner accepted, in words, for the review step to skip.</param>
/// <param name="Sizes">The sizes this screen is captured at, 'WxH'; the config's when null.</param>
/// <param name="SettleMs">How long the design page may settle before its screenshot; default 1000.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopScreen(
    string Name,
    string Route,
    string Design,
    IReadOnlyList<IReadOnlyList<string>>? Fixture = null,
    double? Threshold = null,
    IReadOnlyList<ScreenMask>? Masks = null,
    IReadOnlyList<string>? Accepted = null,
    IReadOnlyList<string>? Sizes = null,
    int? SettleMs = null)
{
    /// <summary>How long the design page settles when the screen says nothing.</summary>
    public const int DefaultSettleMs = 1000;
}

/// <summary>A region that is painted over in both frames before the compare.</summary>
/// <param name="X">Left edge, in pixels.</param>
/// <param name="Y">Top edge, in pixels.</param>
/// <param name="W">Width, in pixels.</param>
/// <param name="H">Height, in pixels.</param>
/// <param name="Why">What changes there between runs (a person reads it).</param>
/// <param name="Size">The 'WxH' size the mask is for; every size when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScreenMask(int X, int Y, int W, int H, string Why, string? Size = null);
