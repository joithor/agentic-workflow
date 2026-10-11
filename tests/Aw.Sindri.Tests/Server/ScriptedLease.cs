using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Tests.Server;

/// <summary>A lease whose renewals answer with <see cref="RenewError"/> when it is set, and counts them.</summary>
internal sealed class ScriptedLease(ILease inner) : ILease
{
    private int _renewals;

    public Error? RenewError { get; set; }

    public int Renewals => Volatile.Read(ref _renewals);

    public ValueTask<Result<LeaseGrant>> Acquire(LeaseHolder candidate, CancellationToken cancellationToken) => inner.Acquire(candidate, cancellationToken);

    public ValueTask<Result<LeaseGrant>> Renew(LeaseGrant grant, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _renewals);
        return RenewError is { } error ? ValueTask.FromResult<Result<LeaseGrant>>(error) : inner.Renew(grant, cancellationToken);
    }

    public ValueTask<Result<Unit>> Release(LeaseGrant grant, CancellationToken cancellationToken) => inner.Release(grant, cancellationToken);

    public ValueTask<Result<LeaseRow?>> Read(CancellationToken cancellationToken) => inner.Read(cancellationToken);
}
