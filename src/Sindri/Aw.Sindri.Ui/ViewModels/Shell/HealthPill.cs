namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The top bar's health pill (UI spec §3.2, §4.1, §11.3): a dot, a glyph and the state's word, then how fresh the views
/// are. It maps the shell's state to words; the connection decides the state.
/// </summary>
internal sealed class HealthPill : ObservableObject
{
    /// <summary>The state's glyph (§11.3): ✓ running, ⌛ stale, ✕ not running or unreadable; none while connecting.</summary>
    public string Glyph { get; } = string.Empty;

    /// <summary>The state's word, or "Sindri not responding: run sindri doctor" after three failed refreshes.</summary>
    public string Word { get; } = string.Empty;

    /// <summary>"updated Ns ago" once connected, "stale" when unreadable, or empty.</summary>
    public string Age { get; } = string.Empty;

    /// <summary>The pill in words (its accessible name): the word, and the age after a separator.</summary>
    public string Text { get; } = string.Empty;

    /// <summary>True when the dot shows a healthy control plane.</summary>
    public bool IsHealthy { get; }

    /// <summary>True when the dot shows stale views.</summary>
    public bool IsStale { get; }

    /// <summary>True when the dot shows nothing to follow: not running, unreadable, or a version mismatch.</summary>
    public bool IsDown { get; }

    /// <summary>Stale: the Refresh button beside the pill (§4.1).</summary>
    public bool IsRefreshShown { get; }
}
