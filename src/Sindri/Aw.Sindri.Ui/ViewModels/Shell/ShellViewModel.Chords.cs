using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The G-chords (screens spec §3, rev 7 §8): G, then a page's key within a second. The chord carries no modifier, so it
/// is the same on every platform; the ⌘ chords (⌘K, ⌘,) are Ctrl on Linux and Windows, which the window maps.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>How long G waits for its second key (rev 7 §8).</summary>
    internal static readonly TimeSpan ChordWait = TimeSpan.FromSeconds(1);

    private ITimer? _chordTimer;

    /// <summary>True while G waits for its second key; the top bar shows <see cref="ChordHint"/>.</summary>
    public bool IsChordPending { get; private set; }

    /// <summary>The top bar's hint while a chord waits: "G …"; empty otherwise.</summary>
    public string ChordHint => IsChordPending ? Labels.GoKey + " …" : string.Empty;

    /// <summary>Home's Needs you list filter (G D sets <see cref="HomeFilter.Decisions"/>; any other page chord clears it).</summary>
    public HomeFilter HomeFilter { get; private set; }

    /// <summary>Feeds the chord one plain key (no modifier, not typed into a text field).</summary>
    /// <param name="key">The key's text: a letter or '.'.</param>
    /// <returns>True when the chord took the key (G, or the second key of a chord). Any other key cancels a waiting chord and is not taken.</returns>
    internal bool PressKey(string key)
    {
        if (!IsChordPending)
        {
            if (!string.Equals(key, Labels.GoKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SetChord(pending: true);
            _chordTimer?.Dispose();
            _chordTimer = _clock.CreateTimer(_ => SetChord(pending: false), state: null, ChordWait, Timeout.InfiniteTimeSpan);
            return true;
        }

        SetChord(pending: false);
        _chordTimer?.Dispose();
        _chordTimer = null;
        if (SecondKey(key) is not { } target)
        {
            return false;
        }

        HomeFilter = target.Filter;
        OnChanged(nameof(HomeFilter));
        GoTo(target.Page);
        return true;
    }

    /// <summary>Cancels a waiting chord (any other unmodified key, Esc).</summary>
    internal void CancelChord() => _ = _clock;   // stub (red)

    private static (PageId Page, HomeFilter Filter)? SecondKey(string key) => key.ToUpperInvariant() switch
    {
        "H" or "N" => (PageId.Home, HomeFilter.All),
        "D" => (PageId.Home, HomeFilter.Decisions),
        "W" => (PageId.Workflows, HomeFilter.All),
        "Q" => (PageId.Work, HomeFilter.All),
        "R" => (PageId.Recipes, HomeFilter.All),
        "C" => (PageId.Context, HomeFilter.All),
        "A" => (PageId.Agents, HomeFilter.All),
        "." or "S" => (PageId.System, HomeFilter.All),
        _ => null,
    };

    private void SetChord(bool pending)
    {
        if (IsChordPending == pending)
        {
            return;
        }

        IsChordPending = pending;
        Changed(nameof(IsChordPending), nameof(ChordHint));
    }
}
