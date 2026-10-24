using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Preferences kept in memory: a capture starts from the defaults and leaves nothing behind.</summary>
internal sealed class MemoryPreferences : IPreferencesStore
{
    private byte[]? _bytes;

    public string Location => "memory:preferences.json";

    public ValueTask<byte[]?> Read(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(_bytes is null ? null : [.. _bytes]);

    public ValueTask Write(byte[] contents, CancellationToken cancellationToken)
    {
        _bytes = [.. contents];
        return ValueTask.CompletedTask;
    }
}
