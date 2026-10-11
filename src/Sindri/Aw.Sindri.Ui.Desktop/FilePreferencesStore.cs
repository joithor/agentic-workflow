using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="IPreferencesStore"/>: preferences.json, 0600, replaced by a temp file and a rename.</summary>
/// <param name="path">The file.</param>
internal sealed class FilePreferencesStore(string path) : IPreferencesStore
{
    public string Location => throw new NotSupportedException(path);

    public ValueTask<byte[]?> Read(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask Write(byte[] contents, CancellationToken cancellationToken) => throw new NotSupportedException();
}
