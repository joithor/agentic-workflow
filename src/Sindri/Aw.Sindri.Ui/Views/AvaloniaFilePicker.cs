using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// <see cref="IFilePicker"/> over Avalonia's storage provider, which every head shares (it belongs to the window, so the app holds it, as it does the
/// launcher). The window is looked up at each request; the size limit and the reading are <see cref="StreamFilePicker"/>'s.
/// </summary>
/// <param name="window">The top-level the request goes through, or null before it shows.</param>
internal sealed class AvaloniaFilePicker(Func<TopLevel?> window) : IFilePicker
{
    private readonly StreamFilePicker _picker = new((title, token) => Choose(window, title, token));

    public Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken) => _picker.Pick(title, limit, cancellationToken);

    private static async Task<ChosenFile?> Choose(Func<TopLevel?> window, string title, CancellationToken cancellationToken)
    {
        if (window() is not { } top)
        {
            return null;
        }

        var chosen = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false }).ConfigureAwait(true);
        if (chosen.Count == 0)
        {
            return null;
        }

        var file = chosen[0];
        var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        return new ChosenFile(file.Name, properties.Size is { } size ? (long)size : null, _ => file.OpenReadAsync(), Owner: file);
    }
}
