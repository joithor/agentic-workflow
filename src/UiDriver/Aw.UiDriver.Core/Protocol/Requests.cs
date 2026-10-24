namespace Aw.UiDriver.Core.Protocol;

/// <summary>The element tree below an element (the window when no selector is given).</summary>
/// <param name="From">Where to start; the first window when null.</param>
/// <param name="Depth">How many levels below it to report; 6 when null.</param>
internal sealed record TreeRequest(Selector? From = null, int? Depth = null)
{
    /// <summary>The depth of a tree that asks for none.</summary>
    internal const int DefaultDepth = 6;
}

/// <summary>Finds the elements a selector matches.</summary>
/// <param name="Selector">What to find.</param>
/// <param name="Limit">The most matches to report; 50 when null.</param>
internal sealed record SearchRequest(Selector Selector, int? Limit = null)
{
    /// <summary>The most matches reported when a search names no limit.</summary>
    internal const int DefaultLimit = 50;
}

/// <summary>The properties of one element.</summary>
/// <param name="Selector">The element.</param>
internal sealed record PropsRequest(Selector Selector);

/// <summary>Clicks an element as a person would: a button is pressed, a toggle flips, a list item is selected, an expander opens or closes.</summary>
/// <param name="Selector">The element.</param>
internal sealed record ClickRequest(Selector Selector);

/// <summary>Types text into a text box (the focused one when no selector is given).</summary>
/// <param name="Text">The text.</param>
/// <param name="Selector">The text box; the focused element when null.</param>
/// <param name="Replace">True replaces what the box holds; false (the default) appends.</param>
internal sealed record TypeRequest(string Text, Selector? Selector = null, bool Replace = false);

/// <summary>Presses a key chord on an element (the focused one when no selector is given).</summary>
/// <param name="Chord">The chord: 'Enter', 'Escape', 'Ctrl+K', 'Ctrl+Shift+P'. The key is an Avalonia Key name.</param>
/// <param name="Selector">The element; the focused element when null.</param>
internal sealed record KeyRequest(string Chord, Selector? Selector = null);

/// <summary>Scrolls a scroll viewer by an offset (the one holding the element, or the element itself when it is one).</summary>
/// <param name="Dx">Pixels to the right (negative: left).</param>
/// <param name="Dy">Pixels down (negative: up).</param>
/// <param name="Selector">The element; the first scroll viewer when null.</param>
internal sealed record ScrollRequest(double Dx, double Dy, Selector? Selector = null);

/// <summary>Waits until an element is in a state.</summary>
/// <param name="Selector">The element.</param>
/// <param name="State">'exists', 'visible' (the default), 'hidden', 'gone', 'enabled', 'disabled', 'textEquals' or 'textContains'.</param>
/// <param name="Text">The text for 'textEquals' and 'textContains'.</param>
/// <param name="TimeoutMs">How long to wait; 10000 when null.</param>
internal sealed record WaitForRequest(Selector Selector, string? State = null, string? Text = null, int? TimeoutMs = null)
{
    /// <summary>How long a wait lasts when the request names no time.</summary>
    internal const int DefaultTimeoutMs = 10_000;
}

/// <summary>A picture of the window, or of one element, as PNG.</summary>
/// <param name="Selector">The element; the window when null.</param>
internal sealed record ScreenshotRequest(Selector? Selector = null);

/// <summary>Reads the window's size, or sets it first.</summary>
/// <param name="Width">The new width, or null to leave it.</param>
/// <param name="Height">The new height, or null to leave it.</param>
internal sealed record WindowRequest(int? Width = null, int? Height = null);

/// <summary>Invokes a named action on an element.</summary>
/// <param name="Selector">The element.</param>
/// <param name="Action">'invoke' (a button's command or click), 'toggle', 'select', 'expand', 'collapse' or 'focus'.</param>
internal sealed record ActionRequest(Selector Selector, string Action);
