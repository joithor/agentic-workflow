using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>The intent layer in context.db (migration 0006_s5b_intent_links).</summary>
internal sealed class SqliteIntentStore(ContextDatabase database) : IIntentStore
{
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken) => throw Pending();

    private NotSupportedException Pending() => new("S-5B slice 2: the intent store is not written yet (" + database.GetType().Name + ")");
}
