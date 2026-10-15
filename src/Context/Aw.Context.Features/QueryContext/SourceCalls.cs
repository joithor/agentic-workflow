using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// Calls one source under the query's deadline: an error or a missed deadline makes it degraded for this query
/// (SND-CONTEXT-010), never an exception the query sees. Cancellation of the query itself still propagates.
/// </summary>
internal static class SourceCalls
{
    internal static async ValueTask<Result<T>> Within<T>(IContextSource source, (TimeSpan Left, TimeProvider Clock) deadline,
        Func<CancellationToken, ValueTask<Result<T>>> call, CancellationToken cancellationToken)
    {
        using var expiry = new CancellationTokenSource(deadline.Left <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : deadline.Left, deadline.Clock);   // the query's clock
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);
        try
        {
            return await call(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ContextErrors.SourceUnavailable.With($"{source.Describe().Id.Value} missed the query's deadline");
        }
    }

    internal static DegradedSource Degraded(SourceId source, Error error, DateTimeOffset since) =>
        new(source, ContextErrors.SourceUnavailable.Code, error.Code == ContextErrors.SourceUnavailable.Code ? error.Message : $"{error.Code}: {error.Message}", since);   // the cause, when it isn't 010 itself
}
