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
    private readonly Func<IServiceProvider, object, CancellationToken, ValueTask<Result<ServiceName?>>> _admit;
    private readonly Func<Func<Type, object>, object, CancellationToken, ValueTask<JobWorkResult>> _run;

    internal JobDefinition(Type request, Func<object, JobKey> key,
        Func<IServiceProvider, object, CancellationToken, ValueTask<Result<ServiceName?>>> admit,
        Func<Func<Type, object>, object, CancellationToken, ValueTask<JobWorkResult>> run)
    {
        Request = request;
        _key = key;
        _admit = admit;
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

    /// <summary>
    /// Checks <paramref name="request"/> inside the gate before the start is accepted (a 404 or 409 the HTTP caller must see synchronously) and says which
    /// background service the job runs, if any (RunService: its events and its work then carry that service's actor).
    /// </summary>
    /// <param name="services">The services of the start's scope, for a check that needs the profile or the build's services.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The service the job runs (or null), or the error that refuses the start.</returns>
    public ValueTask<Result<ServiceName?>> Admit(IServiceProvider services, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(request);
        return _admit(services, request, cancellationToken);
    }

    /// <summary>Runs the work of <paramref name="request"/>, resolving its service through a DI scope the caller owns.</summary>
    /// <param name="resolve">Resolves a service from the scope the caller owns (a delegate, so Features never holds the provider).</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Trips on CancelJob or the host stopping.</param>
    /// <returns>How it ended; a throw is the runner's to map.</returns>
    public ValueTask<JobWorkResult> Run(Func<Type, object> resolve, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(request);
        return _run(resolve, request, cancellationToken);
    }
}
