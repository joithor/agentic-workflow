using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The host's ledger, except that 'context.map.proposed' appends fail while <see cref="Failing"/> is set (a busy ledger after the rows committed).</summary>
internal sealed class FlakyLedger : ILedgerStore
{
    private static readonly Error _failure = SindriErrors.LedgerUnavailable.With("ledger.db: database is locked");

    public bool Failing { get; set; }

    private ILedgerStore Inner { get; set; } = null!;

    /// <summary>Replaces the host's ledger registration with this one wrapping the original.</summary>
    /// <param name="services">The host's services.</param>
    public void Wrap(IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(ILedgerStore));
        _ = services.Remove(original);
        _ = services.AddSingleton<ILedgerStore>(provider =>
        {
            Inner = (ILedgerStore)(original.ImplementationFactory?.Invoke(provider) ?? original.ImplementationInstance ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));
            return this;
        });
    }

    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) =>
        Failing && entry.Envelope.Type == EventTypes.ContextMapProposed ? ValueTask.FromResult<Result<LedgerAppend>>(_failure) : Inner.Append(entry, grant, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) => Inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => Inner.Latest(type, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => Inner.Head(cancellationToken);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => Inner.Schema(cancellationToken);
}
