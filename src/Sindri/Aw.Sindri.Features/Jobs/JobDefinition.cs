using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// What AddJob registers for one job request type (API P6): how to key a start, check it before it is accepted, and run its work. The runner
/// calls <see cref="Run"/> with a DI scope of its own and never resolves the chained service, so the work does not hold the command gate (plan G3).
/// </summary>
public sealed class JobDefinition
{
    private readonly Func<object, JobKey> _key;
    private readonly Func<object, CancellationToken, ValueTask<Result<Unit>>> _precheck;
    private readonly Func<IServiceProvider, object, CancellationToken, ValueTask<JobWorkResult>> _run;

    internal JobDefinition(Type request, Func<object, JobKey> key, Func<object, CancellationToken, ValueTask<Result<Unit>>> precheck,
        Func<IServiceProvider, object, CancellationToken, ValueTask<JobWorkResult>> run)
    {
        Request = request;
        _key = key;
        _precheck = precheck;
        _run = run;
    }

    /// <summary>The request record this definition serves.</summary>
    public Type Request { get; }

    /// <summary>The idempotency key of <paramref name="request"/> within its kind.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key.</returns>
    public JobKey KeyOf(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _key(request);
    }

    /// <summary>Checks <paramref name="request"/> inside the gate before the start is accepted (a 404 or 409 the HTTP caller must see synchronously).</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or the error that refuses the start.</returns>
    public ValueTask<Result<Unit>> Precheck(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _precheck(request, cancellationToken);
    }

    /// <summary>Runs the work of <paramref name="request"/> in <paramref name="services"/> (a scope the caller owns).</summary>
    /// <param name="services">The scope's provider.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Trips on CancelJob or the host stopping.</param>
    /// <returns>How it ended; a throw is the runner's to map.</returns>
    public ValueTask<JobWorkResult> Run(IServiceProvider services, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(request);
        return _run(services, request, cancellationToken);
    }
}
