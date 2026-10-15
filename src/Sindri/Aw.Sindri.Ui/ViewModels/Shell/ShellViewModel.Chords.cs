namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The G-chords (screens spec §3, rev 7 §8): G, then a page's key within a second.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>True while G waits for its second key; the top bar shows <see cref="ChordHint"/>.</summary>
    public bool IsChordPending { get; private set; }

    /// <summary>The top bar's hint while a chord waits: "G …"; empty otherwise.</summary>
    public string ChordHint => IsChordPending ? Labels.GoKey + " …" : string.Empty;

    /// <summary>Home's Needs you list filter (G D sets <see cref="HomeFilter.Decisions"/>; any other Home chord clears it).</summary>
    public HomeFilter HomeFilter { get; private set; }

    /// <summary>Feeds the chord one plain key (no modifier, not typed into a text field).</summary>
    /// <param name="key">The key's text: a letter or '.'.</param>
    /// <returns>True when the chord took the key (G, or the second key of a chord).</returns>
    internal bool PressKey(string key) => throw new NotSupportedException();
}
