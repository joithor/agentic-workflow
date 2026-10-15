namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: the system clipboard, where Copy puts a CLI command (UI spec §7, §4.1 "Copy"). The app's Avalonia adapter serves every head.</summary>
public interface IClipboard
{
    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True once the clipboard holds it; false when the platform refused.</returns>
    public Task<bool> SetText(string text, CancellationToken cancellationToken);
}
