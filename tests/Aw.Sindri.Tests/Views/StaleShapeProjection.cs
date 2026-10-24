using Aw.Kernel;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// Folds 'fixture.put' into the services view. Its first event also stores a row under 'stale' in the profile view that is no job row
/// (a row written under an older shape); every later event reads that key back as a job row first, as a fold reads its earlier row.
/// </summary>
internal sealed class StaleShapeProjection : IProjection
{
    public int Calls { get; private set; }

    public IReadOnlyList<string> Views => [ViewNames.Services, ViewNames.Profile];

    public bool Handles(string eventType) => string.Equals(eventType, ToggleProjection.Put, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        Calls++;
        if (Calls == 1)
        {
            return await rows.Put(ViewNames.Profile, "stale", FixtureProjection.RowFor(recorded), cancellationToken).ConfigureAwait(false);
        }

        var earlier = await rows.Get<JobRow>(ViewNames.Profile, "stale", cancellationToken).ConfigureAwait(false);
        return earlier.IsError
            ? earlier.Error
            : await rows.Put(ViewNames.Services, recorded.IdempotencyKey, FixtureProjection.RowFor(recorded), cancellationToken).ConfigureAwait(false);
    }
}
