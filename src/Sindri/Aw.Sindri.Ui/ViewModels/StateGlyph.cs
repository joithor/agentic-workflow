namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// A state's glyph (UI spec §11.3): the view draws each as a vector icon from Theme/Icons.axaml, always beside the
/// state's word, so a state is never shown by colour or shape alone.
/// </summary>
internal enum StateGlyph
{
    /// <summary>No glyph (connecting).</summary>
    None,

    /// <summary>✓ ok: running, approved.</summary>
    Ok,

    /// <summary>✕ failure: not running, unreadable, a version mismatch.</summary>
    Down,

    /// <summary>⌛ stale.</summary>
    Stale,

    /// <summary>◇ a profile to look at.</summary>
    Profile,
}
