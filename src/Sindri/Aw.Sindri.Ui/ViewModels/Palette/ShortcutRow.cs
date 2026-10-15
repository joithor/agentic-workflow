namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>One line of the `?` sheet (UI spec §8).</summary>
/// <param name="Keys">The keys, as written.</param>
/// <param name="Action">What they do.</param>
internal sealed record ShortcutRow(string Keys, string Action)
{
    /// <summary>The line's accessible name: "Keys: action".</summary>
    public string Name => Keys + ": " + Action;
}
