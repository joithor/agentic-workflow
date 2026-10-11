using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory preferences file: whatever bytes were written last, or none.</summary>
internal sealed class InMemoryPreferencesStore : IPreferencesStore
{
    private byte[]? _bytes;

    public string Location => "memory:preferences.json";

    /// <summary>How many writes were made.</summary>
    public int Writes { get; private set; }

    public ValueTask<byte[]?> Read(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(_bytes is null ? null : [.. _bytes]);

    public ValueTask Write(byte[] contents, CancellationToken cancellationToken)
    {
        _bytes = [.. contents];
        Writes++;
        return ValueTask.CompletedTask;
    }
}
