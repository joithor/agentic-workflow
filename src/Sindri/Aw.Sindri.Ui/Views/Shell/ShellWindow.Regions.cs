using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): F6 and the chord's key names (UI spec §8, §11.2).</summary>
internal sealed partial class ShellWindow
{
    // F6, or ⌘⌥→ and ⌘⌥← on macOS (§8, §11.2): the next region (top bar, page, sheet when open) gets focus: the
    // region itself, so Tab then moves inside it (or its first focusable control when the region itself can't take focus).
    private bool CycleRegion(bool back)
    {
        if (OverlayOpen)
        {
            return true;   // the palette and the ? sheet trap focus: the regions behind the scrim are out of reach (WCAG 2.4.3)
        }

        Control[] regions = [.. _regions.Select(this.Get<Control>).Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled)];
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var current = Array.FindIndex(regions, r => focused is not null && (r == focused || r.IsVisualAncestorOf(focused)));
        var next = regions[(current + (back ? regions.Length - 1 : 1) + regions.Length) % regions.Length];
        var target = next.Focusable ? next : FirstFocusable(next) ?? next;
        return target.Focus(NavigationMethod.Tab);
    }

    /// <summary>The text a plain key feeds the G-chord: a letter or '.', else null.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Its text, or null.</returns>
    internal static string? ChordKey(Key key) =>
        key is >= Key.A and <= Key.Z ? key.ToString() : key is Key.OemPeriod or Key.Decimal ? "." : null;

    /// <summary>The step a key takes through the regions (§8, §11.2): 1 forward, -1 back, null when it isn't a region key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Its modifiers.</param>
    /// <param name="macOS">Whether the app runs on macOS.</param>
    /// <returns>The step, or null.</returns>
    internal static int? RegionStep(Key key, KeyModifiers modifiers, bool macOS) => (key, modifiers) switch
    {
        (Key.F6, KeyModifiers.None) => 1,
        (Key.F6, KeyModifiers.Shift) => -1,
        (Key.Right, KeyModifiers.Meta | KeyModifiers.Alt) when macOS => 1,
        (Key.Left, KeyModifiers.Meta | KeyModifiers.Alt) when macOS => -1,
        _ => null,
    };

    private static InputElement? FirstFocusable(Visual within) => within.GetVisualDescendants().OfType<InputElement>()
        .FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
}
