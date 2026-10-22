using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary><see cref="IFolderPicker"/> over Avalonia's storage provider on a desktop head. The window is looked up at each request; a provider that can't open folders (a browser head) makes it unavailable.</summary>
/// <param name="window">The top-level the request goes through, or null before it shows.</param>
internal sealed class AvaloniaFolderPicker(Func<TopLevel?> window) : IFolderPicker
{
    public bool IsAvailable => window()?.StorageProvider is { CanPickFolder: true };

    public async Task<string?> Pick(string title, CancellationToken cancellationToken)
    {
        if (window() is not { StorageProvider: { CanPickFolder: true } storage })
        {
            return null;
        }

        var chosen = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false }).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        return chosen.Count == 0 ? null : chosen[0].TryGetLocalPath();
    }
}
