using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds 'codeindex.built' into the 'code-index' view (code index spec §8): one row per repo, the newest build; a replayed older event leaves a newer row alone.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class CodeIndexProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.CodeIndex];

    public bool Handles(string eventType) => string.Equals(eventType, EventTypes.CodeIndexBuilt, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        var built = events.Payload<CodeIndexBuilt>(recorded);
        if (built.IsError)
        {
            return built.Error;
        }

        var current = await rows.Get<CodeIndexRow>(ViewNames.CodeIndex, built.Value.Repo, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        return current.Value is { } existing && existing.Sequence >= recorded.Sequence
            ? Unit.Value   // a replay of an older build changes nothing
            : await rows.Put(ViewNames.CodeIndex, built.Value.Repo, Fold(built.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    internal static CodeIndexRow Fold(CodeIndexBuilt built, LedgerEvent recorded) => new(built.Repo, built.Commit, built.Layers, built.Files, built.Symbols, built.Edges,
        built.UnresolvedShare, built.AmbiguousShare, built.EmbeddedPercent, built.Milliseconds, built.Incremental, recorded.Envelope.OccurredAt, recorded.Sequence);
}
