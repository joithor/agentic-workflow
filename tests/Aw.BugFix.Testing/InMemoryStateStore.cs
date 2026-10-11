using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>The in-memory fake for <see cref="IStateStore"/>.</summary>
public sealed class InMemoryStateStore : IStateStore
{
    private readonly Dictionary<string, BugFixState> _states = new(StringComparer.Ordinal);
    private readonly HashSet<string> _corrupt = new(StringComparer.Ordinal);

    /// <summary>Gets how many times a state was saved.</summary>
    public int Saves { get; private set; }

    /// <summary>Makes the state in <paramref name="directory"/> unreadable.</summary>
    /// <param name="directory">The state directory.</param>
    public void Corrupt(string directory) => _corrupt.Add(directory);

    /// <summary>Gets the state directories whose lock is held.</summary>
    public ISet<string> Held { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Result<IDisposable> Lock(string directory) =>
        Held.Add(directory)
            ? new Release(() => Held.Remove(directory))
            : BugFixErrors.StateBusy.With($"another bugfix-state call holds {directory}/state.lock; retry when it finishes");

    /// <inheritdoc/>
    public string StateFile(string directory) => $"{directory.TrimEnd('/')}/state.json";

    /// <inheritdoc/>
    public Result<BugFixState> Load(string directory)
    {
        if (_corrupt.Contains(directory))
        {
            return BugFixErrors.StateCorrupt.With($"invalid state file {StateFile(directory)}");
        }

        return _states.TryGetValue(directory, out var state)
            ? state
            : BugFixErrors.StateMissing.With($"no state at {StateFile(directory)}; run bugfix-state init first");
    }

    /// <inheritdoc/>
    public void Save(string directory, BugFixState state)
    {
        _states[directory] = state;
        _ = _corrupt.Remove(directory);
        Saves++;
    }

    private sealed class Release(Func<bool> release) : IDisposable
    {
        public void Dispose() => _ = release();
    }
}
