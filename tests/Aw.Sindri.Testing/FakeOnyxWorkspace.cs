using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The workspace in memory. Held to <see cref="OnyxWorkspaceContract"/>.</summary>
public sealed class FakeOnyxWorkspace : IOnyxWorkspace
{
    /// <summary>Gets the text files by path.</summary>
    public IDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the state; null reads as missing.</summary>
    public OnyxSetupState? State { get; set; }

    /// <summary>Gets or sets a value indicating whether the state file is corrupt (it reads as null until a state is written).</summary>
    public bool StateIsCorrupt { get; set; }

    /// <summary>Gets the paths written, in order (a write that changed nothing isn't listed), and whether each was written as a protected file.</summary>
    public IList<(string Path, bool Protected)> Writes { get; } = [];

    /// <summary>Gets a value indicating whether the lock is held.</summary>
    public bool LockHeld { get; private set; }

    /// <summary>Gets the number of state writes.</summary>
    public int StateWrites { get; private set; }

    /// <inheritdoc />
    public string? Read(string path) => Files.TryGetValue(path, out var text) ? text : null;

    /// <inheritdoc />
    public bool WriteIfChanged(string path, string content, bool secret)
    {
        if (Files.TryGetValue(path, out var have) && string.Equals(have, content, StringComparison.Ordinal))
        {
            return false;
        }

        Files[path] = content;
        Writes.Add((path, secret));
        return true;
    }

    /// <inheritdoc />
    public OnyxSetupState? ReadState() => StateIsCorrupt ? null : State;

    /// <inheritdoc />
    public bool StateIsUnreadable() => StateIsCorrupt;

    /// <inheritdoc />
    public void WriteState(OnyxSetupState state)
    {
        (State, StateIsCorrupt) = (state, false);
        StateWrites++;
    }

    /// <inheritdoc />
    public IDisposable? TryLock()
    {
        if (LockHeld)
        {
            return null;
        }

        LockHeld = true;
        return new Release(this);
    }

    private sealed class Release(FakeOnyxWorkspace owner) : IDisposable
    {
        public void Dispose() => owner.LockHeld = false;
    }
}
