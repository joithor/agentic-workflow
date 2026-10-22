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

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    internal static CodeIndexRow Fold(CodeIndexBuilt built, LedgerEvent recorded) =>
        throw new NotSupportedException();
}
