using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// <see cref="Platform.IClipboard"/> over Avalonia's clipboard, which every head shares (it belongs to the window, so the app
/// holds it, not a head). The window is looked up at copy time: it is created after the view models.
/// </summary>
/// <param name="window">The top-level the copy goes through, or null before it shows.</param>
internal sealed class AvaloniaClipboard(Func<TopLevel?> window) : Platform.IClipboard
{
    public async Task<bool> SetText(string text, CancellationToken cancellationToken)
    {
        if (window()?.Clipboard is not { } clipboard)
        {
            return false;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
        return true;
    }
}
