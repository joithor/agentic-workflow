using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>Folds 'fixture.put' into the services view like FixtureProjection; a test can make it fail or throw, and it counts its calls.</summary>
internal sealed class ToggleProjection : IProjection
{
    public const string Put = "fixture.put";

    public bool Fail { get; set; }

    public bool Throw { get; set; }

    public int Calls { get; private set; }

    public IReadOnlyList<string> Views => [ViewNames.Services];

    public bool Handles(string eventType) => string.Equals(eventType, Put, StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throw)
        {
            throw new InvalidOperationException("fixture: the fold threw");
        }

        return Fail
            ? ValueTask.FromResult<Result<Unit>>(FixtureProjection.Failure)
            : rows.Put(ViewNames.Services, recorded.IdempotencyKey, FixtureProjection.RowFor(recorded), cancellationToken);
    }
}
