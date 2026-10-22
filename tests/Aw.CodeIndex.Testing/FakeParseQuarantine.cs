using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>An in-memory <see cref="IParseQuarantine"/>; <see cref="Restarted"/> is a new instance over the same state, the way a restart finds the files of the run before.</summary>
public sealed class FakeParseQuarantine : IParseQuarantine
{
    private readonly State _state;

    /// <summary>Creates an empty quarantine.</summary>
    public FakeParseQuarantine()
        : this(new State())
    {
    }

    private FakeParseQuarantine(State state)
    {
        _state = state;
    }

    /// <summary>What a process that starts after this one died sees: the same marks and lists.</summary>
    /// <returns>A new instance over the same state.</returns>
    public FakeParseQuarantine Restarted() => new(_state);

    /// <inheritdoc />
    public int Recover(RepoName repo)
    {
        var list = ListOf(repo);
        if (_state.InFlight.Remove(repo.Value, out var blob) && !list.Contains(blob))
        {
            list.Add(blob);
            Trim(list);
        }

        return list.Count;
    }

    /// <inheritdoc />
    public bool Contains(RepoName repo, string blob) => ListOf(repo).Contains(blob);

    /// <inheritdoc />
    public void Begin(RepoName repo, string blob)
    {
        if (blob.Length is < 40 or > 64 || !blob.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A blob id is 40 to 64 hex digits.", nameof(blob));
        }

        _state.InFlight[repo.Value] = blob;
    }

    /// <inheritdoc />
    public void End(RepoName repo) => _state.InFlight.Remove(repo.Value);

    /// <inheritdoc />
    public int Keep(RepoName repo, IReadOnlySet<string> blobsInHead)
    {
        var list = ListOf(repo);
        _ = list.RemoveAll(blob => !blobsInHead.Contains(blob));
        return list.Count;
    }

    private static void Trim(List<string> list)
    {
        if (list.Count > CodeIndexRules.MaxQuarantined)
        {
            list.RemoveRange(0, list.Count - CodeIndexRules.MaxQuarantined);
        }
    }

    private List<string> ListOf(RepoName repo)
    {
        if (!_state.Lists.TryGetValue(repo.Value, out var list))
        {
            _state.Lists[repo.Value] = list = [];
        }

        return list;
    }

    private sealed class State
    {
        public Dictionary<string, string> InFlight { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<string>> Lists { get; } = new(StringComparer.Ordinal);
    }
}
