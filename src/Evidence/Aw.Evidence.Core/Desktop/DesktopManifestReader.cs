using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Reads a screens manifest and checks its rules.</summary>
public static partial class DesktopManifestReader
{
    /// <summary>Parses and validates a screens manifest.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The manifest, or AW-EVIDENCE-026 naming every problem.</returns>
    public static Result<DesktopManifest> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<DesktopManifest>(utf8Json, options, EvidenceErrors.DesktopManifestInvalid).Bind(Validate);

    private static Result<DesktopManifest> Validate(DesktopManifest manifest)
    {
        var problems = new Problems();
        problems.AddIf(manifest.Schema != DesktopManifest.SchemaName, $"$.schema: must be {DesktopManifest.SchemaName}");
        problems.AddIf(string.IsNullOrWhiteSpace(manifest.DesignRoot) || manifest.DesignRoot.StartsWith('/'), "$.designRoot: must be a folder relative to the manifest's folder ('.' for the folder itself)");
        problems.AddIf(manifest.Threshold is < 0 or > 100, "$.threshold: must be 0 to 100");
        problems.AddIf(manifest.Screens is null || manifest.Screens.Count == 0, "$.screens: must list at least one screen");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var screen in (manifest.Screens ?? []).Where(static s => s is not null))
        {
            problems.AddIf(!seen.Add(screen.Name ?? string.Empty), $"$.screens: the name '{screen.Name}' is used twice");
            CheckScreen(screen, problems);
        }

        problems.AddIf((manifest.Screens ?? []).Any(static s => s is null), "$.screens: holds a null entry");
        return problems.Into(manifest, EvidenceErrors.DesktopManifestInvalid);
    }

    private static void CheckScreen(DesktopScreen screen, Problems problems)
    {
        var at = $"$.screens[{screen.Name}]";
        problems.AddIf(string.IsNullOrEmpty(screen.Name) || !ScreenName.IsMatch(screen.Name), $"{at}.name: lowercase letters, digits and '-', at most 64");
        problems.AddIf(string.IsNullOrEmpty(screen.Route) || screen.Route.Any(char.IsWhiteSpace), $"{at}.route: must not be empty or hold spaces");
        problems.AddIf(!DesktopTargetsRules.IsRepoRelative(screen.Design) || !screen.Design.EndsWith(".html", StringComparison.Ordinal), $"{at}.design: must be an .html file below the design root");
        problems.AddIf(screen.Threshold is < 0 or > 100, $"{at}.threshold: must be 0 to 100");
        problems.AddIf(screen.SettleMs is < 0 or > 30_000, $"{at}.settleMs: must be 0 to 30000");
        problems.AddIf(screen.Fixture is { } steps && steps.Any(static s => s is null || s.Count == 0 || s.Any(string.IsNullOrEmpty)), $"{at}.fixture: each step must list its arguments, none empty");
        problems.AddIf(screen.Accepted is { } accepted && accepted.Any(string.IsNullOrWhiteSpace), $"{at}.accepted: an entry must not be empty");
        foreach (var size in screen.Sizes ?? [])
        {
            problems.AddIf(!SizeText.TryParse(size, out _), $"{at}.sizes: '{size}' is not a size like 1440x900");
        }

        foreach (var mask in screen.Masks ?? [])
        {
            problems.AddIf(mask is null || mask.W < 1 || mask.H < 1 || mask.X < 0 || mask.Y < 0, $"{at}.masks: a mask needs x, y of 0 or more and w, h of 1 or more");
            problems.AddIf(mask is not null && string.IsNullOrWhiteSpace(mask.Why), $"{at}.masks: a mask needs a 'why'");
            problems.AddIf(mask?.Size is { } size && !SizeText.TryParse(size, out _), $"{at}.masks: size '{mask?.Size}' is not a size like 1440x900");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ScreenName { get; }
}
