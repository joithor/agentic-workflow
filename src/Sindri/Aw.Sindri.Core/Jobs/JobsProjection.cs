using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Jobs;

/// <summary>Folds the four job events into the 'jobs' view (API P6): one row per job, keyed by its id; the pure folds are <see cref="JobRules"/>.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class JobsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Jobs];

    public bool Handles(string eventType) => throw new NotSupportedException("red until API P6 unit A");

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red until API P6 unit A {events.Scope}");
}
