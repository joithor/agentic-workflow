using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// <see cref="IFilePicker"/> over Avalonia's storage provider, which every head shares (it belongs to the window, so the app holds it, as it does the
/// launcher). The window is looked up at each request. A file larger than the limit is reported by its size and left unread; a refusal is no choice.
/// </summary>
/// <param name="window">The top-level the request goes through, or null before it shows.</param>
internal sealed class AvaloniaFilePicker(Func<TopLevel?> window) : IFilePicker
{
    public async Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken)
    {
        try
        {
            if (window() is not { } top)
            {
                return null;
            }

            var chosen = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false }).ConfigureAwait(true);
            return chosen.Count == 0 ? null : await Read(chosen[0], limit, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return null;   // the platform's refusal is an answer, not a fault
        }
    }

    private static async Task<PickedFile> Read(IStorageFile file, int limit, CancellationToken cancellationToken)
    {
        var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);
        var known = properties.Size is { } size ? (long)size : -1;
        if (known > limit)
        {
            return new PickedFile(file.Name, known, ReadOnlyMemory<byte>.Empty);
        }

        var buffer = new byte[limit + 1];
        var read = 0;
        var stream = await file.OpenReadAsync().ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            while (read < buffer.Length && await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(true) is var n and > 0)
            {
                read += n;
            }
        }

        return read > limit ? new PickedFile(file.Name, read, ReadOnlyMemory<byte>.Empty) : new PickedFile(file.Name, read, buffer.AsMemory(0, read));
    }
}
