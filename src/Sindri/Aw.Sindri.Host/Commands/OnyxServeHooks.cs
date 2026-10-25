using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxEnsure;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Onyx beside `sindri serve` (spec §4.2, O4), only when the approved profile says Sindri manages it: <see cref="Start"/> runs in the background once the control plane is listening (it never blocks the API and never fails serve; any other failure is told on standard error) and records
/// what it found in control.json beside Temporal; <see cref="Stop"/> runs `compose stop` when serve is told to stop, before the record and the lease go (so a successor serve never has its Onyx stopped by this one), bounded. An unmanaged Onyx is never touched.
/// </summary>
internal static class OnyxServeHooks
{
    private static readonly TimeSpan _startWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _stopBudget = TimeSpan.FromSeconds(60);

    /// <summary>Brings the managed Onyx up and rewrites control.json with its state.</summary>
    /// <param name="app">The running host.</param>
    /// <param name="endpoint">What serve wrote to control.json.</param>
    /// <param name="endpoints">The control.json file.</param>
    /// <param name="cancellationToken">Ends with serve.</param>
    /// <returns>A task that ends when Onyx answers, failed, or serve stops; it never throws.</returns>
    internal static Task Start(WebApplication app, ControlEndpoint endpoint, IEndpointFile endpoints, CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            try
            {
                var answer = await InScope<OnyxEnsureRequest, OnyxEnsureResponse>(app, new OnyxEnsureRequest(), cancellationToken).ConfigureAwait(false);
                if (answer is { IsSuccess: true, Value.Endpoint: { } onyx } && !cancellationToken.IsCancellationRequested)
                {
                    _ = await endpoints.Write(endpoint with { Onyx = onyx }, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // serve is stopping: the stop hook below takes it from here
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                await Console.Error.WriteLineAsync($"serve could not bring Onyx up: {e.Message}").ConfigureAwait(false);
            }
        }, CancellationToken.None);

    /// <summary>Waits for <paramref name="started"/> to give up, then stops the managed Onyx's containers (kept volumes), bounded (about a minute); skipped when this serve no longer holds the lease; a timeout or a failure is reported on standard error, never fatal.</summary>
    /// <param name="app">The host.</param>
    /// <param name="started">The task <see cref="Start"/> returned.</param>
    /// <param name="error">Where a failure is told.</param>
    /// <returns>A task that ends when the stop is done or its budget is spent.</returns>
    internal static async Task Stop(WebApplication app, Task started, TextWriter error)
    {
        if (app.Services.GetRequiredService<HeldLease>().Current is null)
        {
            return;   // the lease is lost: a successor serve may own the Onyx now, so this one leaves it alone
        }

        using var budget = new CancellationTokenSource(_stopBudget);
        try
        {
            // Start has seen serve's token cancelled; it gets a short while to give up before the containers are stopped
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
            wait.CancelAfter(_startWait);
            try
            {
                await started.WaitAsync(wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!budget.IsCancellationRequested)
            {
                // Start is still unwinding: stop the containers anyway
            }

            var scope = app.Services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var managed = await scope.ServiceProvider.GetRequiredService<IManagedOnyx>().Is(budget.Token).ConfigureAwait(false);
                if (managed is { IsSuccess: true, Value: true })
                {
                    var stopped = await InScope<OnyxDownRequest, OnyxDownResponse>(app, new OnyxDownRequest(), budget.Token).ConfigureAwait(false);
                    if (stopped.IsError)
                    {
                        await error.WriteLineAsync($"{stopped.Error.Code} serve could not stop Onyx: {stopped.Error.Message}").ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("serve stopped waiting for Onyx to stop; run `sindri onyx down`").ConfigureAwait(false);
        }
    }

    private static async Task<Kernel.Result<TRes>> InScope<TReq, TRes>(WebApplication app, TReq request, CancellationToken cancellationToken)
        where TReq : notnull
    {
        using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))
        {
            var scope = app.Services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await scope.ServiceProvider.GetRequiredService<IService<TReq, TRes>>().Handle(request, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
