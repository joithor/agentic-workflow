using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// The app's local preferences (UI spec §12.3): presentation only, never logic. Read values are clamped
/// (<see cref="Clamp"/>); a file that is malformed or of another <see cref="Version"/> reads as <see cref="Defaults"/>.
/// </summary>
/// <param name="Version">The file format; <see cref="CurrentVersion"/>.</param>
/// <param name="Window">The main window's bounds, or null to let the platform place it.</param>
/// <param name="SheetWidth">The detail sheet's width (<see cref="MinSheetWidth"/>–<see cref="MaxSheetWidth"/>).</param>
/// <param name="LastPage">The page open at exit: Home, Queue, Decisions, Services, Health or Settings.</param>
/// <param name="SeenIds">Needs-you row ids already seen (§5), newest last, at most <see cref="MaxSeenIds"/>.</param>
/// <param name="PaletteHistory">Command-palette entries run, newest last, at most <see cref="MaxPaletteHistory"/>.</param>
/// <param name="TextScale">The type ramp's scale (<see cref="MinTextScale"/>–<see cref="MaxTextScale"/>).</param>
/// <param name="ReduceMotion">The in-app Reduce motion switch: forces the reduced path whatever the OS says.</param>
public sealed record UiPreferences(
    int Version,
    WindowBounds? Window,
    double SheetWidth,
    string LastPage,
    IReadOnlyList<string> SeenIds,
    IReadOnlyList<string> PaletteHistory,
    double TextScale,
    bool ReduceMotion)
{
    /// <summary>The format this build writes and reads.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The window's minimum width (§3.2).</summary>
    public const double MinWidth = 900;

    /// <summary>The window's minimum height (§3.2).</summary>
    public const double MinHeight = 600;

    /// <summary>The detail sheet's default width (§3.2).</summary>
    public const double DefaultSheetWidth = 520;

    /// <summary>The detail sheet's narrowest width.</summary>
    public const double MinSheetWidth = 420;

    /// <summary>The detail sheet's widest width.</summary>
    public const double MaxSheetWidth = 760;

    /// <summary>The smallest text scale (§14.2).</summary>
    public const double MinTextScale = 0.9;

    /// <summary>The largest text scale.</summary>
    public const double MaxTextScale = 1.3;

    /// <summary>The most seen ids kept (the oldest go first).</summary>
    public const int MaxSeenIds = 500;

    /// <summary>The most palette entries kept (the oldest go first).</summary>
    public const int MaxPaletteHistory = 20;

    /// <summary>The page the app opens on with no preference.</summary>
    public const string DefaultPage = "Home";

    /// <summary>The pages <see cref="LastPage"/> may name.</summary>
    public static IReadOnlyList<string> Pages { get; } = ["Home", "Queue", "Decisions", "Services", "Health", "Settings"];

    /// <summary>The preferences of a first run.</summary>
    public static UiPreferences Defaults { get; } = new(CurrentVersion, Window: null, DefaultSheetWidth, DefaultPage, [], [], TextScale: 1.0, ReduceMotion: false);

    /// <summary>Parses a preferences file.</summary>
    /// <param name="json">The file's bytes.</param>
    /// <returns>The clamped preferences, or <see cref="Defaults"/> when the file is malformed or of another version.</returns>
    public static UiPreferences FromJson(ReadOnlySpan<byte> json)
    {
        var file = AwJson.Deserialize<PreferencesFile>(json, SindriApiClient.Json);
        if (file.IsError || file.Value.Version != CurrentVersion)
        {
            return Defaults;
        }

        var f = file.Value;
        return new UiPreferences(
            f.Version,
            f.Window,
            f.SheetWidth ?? DefaultSheetWidth,
            f.LastPage ?? DefaultPage,
            f.SeenIds ?? [],
            f.PaletteHistory ?? [],
            f.TextScale ?? Defaults.TextScale,
            f.ReduceMotion ?? Defaults.ReduceMotion).Clamp();
    }

    /// <summary>The preferences as the file's bytes (UTF-8 JSON, camelCase), clamped first, so a file never holds a value out of range.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToJson()
    {
        var p = Clamp();
        return AwJson.Serialize(new PreferencesFile(p.Version, p.Window, p.SheetWidth, p.LastPage, p.SeenIds, p.PaletteHistory, p.TextScale, p.ReduceMotion), SindriApiClient.Json);
    }

    /// <summary>
    /// These preferences within their limits: bounds below the window minimum grow to it (bounds that aren't finite are
    /// dropped), the sheet width and text scale are clamped (not finite: their defaults), an unknown page reads as
    /// <see cref="DefaultPage"/>, blank and repeated ids are dropped and the lists keep their newest entries.
    /// </summary>
    /// <returns>The clamped preferences.</returns>
    public UiPreferences Clamp() => new(
        CurrentVersion,
        Window is { } w && double.IsFinite(w.X) && double.IsFinite(w.Y) && double.IsFinite(w.Width) && double.IsFinite(w.Height)
            ? w with { Width = Math.Max(w.Width, MinWidth), Height = Math.Max(w.Height, MinHeight) }
            : null,
        double.IsFinite(SheetWidth) ? Math.Clamp(SheetWidth, MinSheetWidth, MaxSheetWidth) : DefaultSheetWidth,
        Pages.Contains(LastPage, StringComparer.Ordinal) ? LastPage : DefaultPage,
        Newest(SeenIds, MaxSeenIds),
        Newest(PaletteHistory, MaxPaletteHistory),
        double.IsFinite(TextScale) ? Math.Clamp(TextScale, MinTextScale, MaxTextScale) : Defaults.TextScale,
        ReduceMotion);

    // The non-blank entries, each once (at its newest place), keeping the newest 'max'.
    private static string[] Newest(IReadOnlyList<string>? entries, int max)
    {
        var kept = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = (entries?.Count ?? 0) - 1; i >= 0 && kept.Count < max; i--)
        {
            var entry = entries![i]; // why: Count is 0 when null, so the loop doesn't run
            if (!string.IsNullOrWhiteSpace(entry) && seen.Add(entry))
            {
                kept.Add(entry);
            }
        }

        kept.Reverse();
        return [.. kept];
    }

    /// <inheritdoc />
    public bool Equals(UiPreferences? other) =>
        other is not null
        && Version == other.Version
        && Equals(Window, other.Window)
        && SheetWidth.Equals(other.SheetWidth)
        && string.Equals(LastPage, other.LastPage, StringComparison.Ordinal)
        && SeenIds.SequenceEqual(other.SeenIds, StringComparer.Ordinal)
        && PaletteHistory.SequenceEqual(other.PaletteHistory, StringComparer.Ordinal)
        && TextScale.Equals(other.TextScale)
        && ReduceMotion == other.ReduceMotion;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Version, Window, SheetWidth, LastPage, SeenIds.Count, PaletteHistory.Count, TextScale, ReduceMotion);
}
