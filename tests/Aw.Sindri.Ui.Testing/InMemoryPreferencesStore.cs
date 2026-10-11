using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory preferences file: bytes written and parsed the way every store does (<see cref="UiPreferences.ToJson"/>, <see cref="UiPreferences.FromJson"/>).</summary>
internal sealed class InMemoryPreferencesStore : IPreferencesStore
{
    private byte[]? _bytes;

    /// <summary>How many writes were made.</summary>
    public int Writes { get; private set; }

    /// <summary>Puts raw bytes in the file (a damaged or foreign one).</summary>
    /// <param name="bytes">The bytes.</param>
    public void WriteRaw(byte[] bytes) => _bytes = [.. bytes];

    public ValueTask<UiPreferences> Read(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_bytes is null ? UiPreferences.Defaults : UiPreferences.FromJson(_bytes));

    public ValueTask Write(UiPreferences preferences, CancellationToken cancellationToken)
    {
        _bytes = preferences.ToJson();
        Writes++;
        return ValueTask.CompletedTask;
    }
}
