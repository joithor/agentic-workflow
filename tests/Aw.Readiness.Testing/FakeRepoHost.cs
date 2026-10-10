using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The fake of <see cref="IRepoHost"/>: answers with the facts it was given and counts the reads.</summary>
/// <param name="facts">What every read returns.</param>
public sealed class FakeRepoHost(HostFacts facts) : IRepoHost
{
    /// <summary>How many times <see cref="Read"/> ran.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<HostFacts> Read(string root, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(facts);
    }
}
