using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The in-memory preferences file with gates a test holds shut: while <see cref="HoldReads"/> (or <see cref="HoldWrites"/>)
/// is in force, a read (or a write) waits until <see cref="ReleaseReads"/> (or <see cref="ReleaseWrites"/>), so a test can
/// overlap two writers' read-change-write, or keep a write pending.
/// </summary>
internal sealed class GatedPreferencesStore : IPreferencesStore
{
    private readonly InMemoryPreferencesStore _file = new();
    private TaskCompletionSource _reads = Open();
    private TaskCompletionSource _writes = Open();

    public string Location => _file.Location;

    /// <summary>How many writes were made.</summary>
    public int Writes => _file.Writes;

    /// <summary>Makes the next reads wait.</summary>
    public void HoldReads() => _reads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the waiting reads (and later ones) through.</summary>
    public void ReleaseReads() => _reads.TrySetResult();

    /// <summary>Makes the next writes wait.</summary>
    public void HoldWrites() => _writes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the waiting writes (and later ones) through.</summary>
    public void ReleaseWrites() => _writes.TrySetResult();

    public async ValueTask<byte[]?> Read(CancellationToken cancellationToken)
    {
        await _reads.Task.WaitAsync(cancellationToken);
        return await _file.Read(cancellationToken);
    }

    public async ValueTask Write(byte[] contents, CancellationToken cancellationToken)
    {
        await _writes.Task.WaitAsync(cancellationToken);
        await _file.Write(contents, cancellationToken);
    }

    private static TaskCompletionSource Open()
    {
        var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        open.SetResult();
        return open;
    }
}
