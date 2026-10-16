using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Linking;

/// <summary>The in-memory intent store; <see cref="IntentStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeIntentStore : IIntentStore
{
    /// <inheritdoc/>
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken) => throw Pending();

    /// <inheritdoc/>
    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken) => throw Pending();

    /// <inheritdoc/>
    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken) => throw Pending();

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken) => throw Pending();

    private static NotSupportedException Pending() => new("S-5B slice 2: the fake intent store is not written yet");
}
