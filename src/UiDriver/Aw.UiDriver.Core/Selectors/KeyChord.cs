namespace Aw.UiDriver.Core.Selectors;

/// <summary>A key chord read from text: the key's name and the modifiers held with it.</summary>
/// <param name="Key">The key's name (an Avalonia Key member, as written).</param>
/// <param name="Control">Ctrl is held.</param>
/// <param name="Shift">Shift is held.</param>
/// <param name="Alt">Alt is held.</param>
/// <param name="Meta">The platform key (Cmd, Win, Super) is held.</param>
internal sealed record KeyChord(string Key, bool Control, bool Shift, bool Alt, bool Meta)
{
    /// <summary>Reads 'Ctrl+Shift+K'. Modifier names (Ctrl, Control, Shift, Alt, Meta, Cmd, Command, Win, Super) come before the key; the key is the last part.</summary>
    /// <param name="text">The chord.</param>
    /// <param name="chord">The chord, when the text is one.</param>
    /// <returns>True for a chord with a key and only known modifiers, none twice.</returns>
    internal static bool TryParse(string? text, out KeyChord chord) => throw new NotSupportedException();
}
