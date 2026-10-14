using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>Installed harness versions in memory: a command missing from the table isn't installed.</summary>
public sealed class FakeHarnessVersions : IHarnessVersions
{
    private readonly Dictionary<string, string> _versions = new(StringComparer.Ordinal);
    private readonly List<string> _reads = [];

    /// <summary>The commands read, in order.</summary>
    public IReadOnlyList<string> Reads => _reads;

    /// <summary>Installs <paramref name="command"/> at <paramref name="version"/>.</summary>
    /// <param name="command">The harness command.</param>
    /// <param name="version">What '--version' reports.</param>
    /// <returns>This fake.</returns>
    public FakeHarnessVersions With(string command, string version)
    {
        _versions[command] = version;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<string?> Read(string command, CancellationToken cancellationToken)
    {
        _reads.Add(command);
        return ValueTask.FromResult(_versions.TryGetValue(command, out var version) ? version : null);
    }
}
