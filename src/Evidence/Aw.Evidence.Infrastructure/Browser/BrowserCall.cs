using Aw.Evidence.Core;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>Runs one browser call and turns any Playwright failure (missing element, timeout, crash) into AW-EVIDENCE-013.</summary>
internal static class BrowserCall
{
    /// <summary>Runs <paramref name="call"/>.</summary>
    /// <param name="what">What the call does, for the error.</param>
    /// <param name="call">The call.</param>
    /// <param name="cancellationToken">
    /// Observed for the whole call: Playwright calls take no token, so a cancellation stops the wait at once and abandons
    /// the call (the session's dispose closes its context, which ends it).
    /// </param>
    /// <param name="redact">Scrubs secrets out of an error message.</param>
    /// <typeparam name="T">The result.</typeparam>
    /// <returns>The result, or AW-EVIDENCE-013.</returns>
    internal static async ValueTask<Result<T>> Run<T>(string what, Func<Task<T>> call, CancellationToken cancellationToken, Func<string, string>? redact = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<T>? running = null;
        try
        {
            running = call();
            return await running.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && running is { IsCompleted: false })
        {
            // The abandoned call fails once its context closes; observe that so it isn't an unobserved task exception.
            _ = running.ContinueWith(static t => t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = $"{what}: {FirstLine(ex.Message)}";
            return EvidenceErrors.BrowserFailed.With(redact is null ? message : redact(message));
        }
    }

    /// <summary>Runs <paramref name="call"/>, which returns nothing.</summary>
    /// <param name="what">What the call does, for the error.</param>
    /// <param name="call">The call.</param>
    /// <param name="cancellationToken">Observed for the whole call.</param>
    /// <returns>Success, or AW-EVIDENCE-013.</returns>
    internal static ValueTask<Result<Unit>> Run(string what, Func<Task> call, CancellationToken cancellationToken) =>
        Run(what, async () =>
        {
            await call().ConfigureAwait(false);
            return Unit.Value;
        }, cancellationToken);

    // Playwright appends a call log after the first line; the first line says what failed.
    private static string FirstLine(string message)
    {
        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).Trim();
    }
}
