using System.Globalization;
using System.Net;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Kernel.Json;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Sends the onyx adapter's calls with the key and reads Onyx v4.9.0's replies into typed records. The key comes from
/// calling the environment with the settings' pointer text (Sindri resolves pointers, spec §4); it goes only into the
/// Authorization header, never into an error or a log line. Failures map to the catalogue: no usable key 002, no
/// answer in time or a 5xx 010, a 403 050, a 404 on a document route 052, any other status or shape 051.
/// </summary>
/// <param name="settings">The checked settings.</param>
/// <param name="parts">The client, environment, clock and log every onyx source shares.</param>
internal sealed partial class OnyxClient(OnyxSettings settings, OnyxSourceParts parts)
{
    private const string TokenKey = "context.sources.onyx.token";
    private readonly OnyxSourceParts _parts = parts;

    /// <summary>The checked settings.</summary>
    public OnyxSettings Settings { get; } = settings;

    /// <summary>The HTTP client.</summary>
    public HttpClient Http => _parts.Http;

    /// <summary>Sends one call with the key and reads the reply.</summary>
    /// <param name="call">The call (allowlisted by <see cref="OnyxRequests"/>).</param>
    /// <param name="deadline">How long Onyx gets to answer.</param>
    /// <param name="cancellationToken">The caller's cancellation, which is thrown, not turned into an error.</param>
    /// <typeparam name="T">The reply record.</typeparam>
    /// <returns>The reply, or an error from the catalogue.</returns>
    public async ValueTask<Result<T>> Call<T>(OnyxCall call, TimeSpan deadline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var key = _parts.Environment(Settings.Token);
        if (string.IsNullOrEmpty(key) || key.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return Failed(call, ContextErrors.SourceNotConfigured.With(
                $"{TokenKey}: the secret pointer doesn't resolve to a usable Onyx key (none, or one with spaces or control characters). "
                + "Set it to a dedicated service account's admin key (spec §8.8) and approve the profile again."), status: null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            var reply = await OnyxRequests.Send(_parts.Http, Settings.Url, call, key, timeout.Token).ConfigureAwait(false);
            return Read<T>(call, reply);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(call, Unavailable(string.Create(CultureInfo.InvariantCulture,
                $"didn't answer {call.Route} within {deadline.TotalSeconds:0.###} s")), status: null);
        }
        catch (HttpRequestException e)
        {
            return Failed(call, Unavailable($"isn't reachable ({e.HttpRequestError}); start Onyx or check context.sources.onyx.url"), status: null);
        }
    }

    private Result<T> Read<T>(OnyxCall call, OnyxReply reply)
    {
        var status = (int)reply.Status;
        if (status is >= 200 and < 300)
        {
            var value = AwJson.Deserialize<T>(reply.Body, OnyxWire.Options);
            return value.IsSuccess ? value : Failed(call, Unexpected($"answered {call.Route} with a body"), status);
        }

        return Failed(call, StatusError(call, reply.Status), status);
    }

    private Error StatusError(OnyxCall call, HttpStatusCode status)
    {
        var answered = string.Create(CultureInfo.InvariantCulture, $"answered HTTP {(int)status} on {call.Route}");
        if (status == HttpStatusCode.Forbidden)
        {
            return ContextErrors.OnyxKeyRefused.With(
                $"Onyx at {Settings.Address} refused the key on {call.Method} {call.Route} (HTTP 403): it is missing, wrong, or lacks the admin "
                + $"role POST /api/admin/search needs. Create an admin key for a dedicated service account, store it behind {TokenKey} "
                + "and approve the profile again (spec §8.8).");
        }

        if (status == HttpStatusCode.NotFound && call.Route.StartsWith("/api/document/", StringComparison.Ordinal))
        {
            return ContextErrors.OnyxDocumentNotFound.With($"Onyx at {Settings.Address} has no such document ({call.Route} answered 404).");
        }

        return (int)status >= 500 ? Unavailable(answered) : Unexpected(answered);
    }

    private Error Unavailable(string what) => ContextErrors.SourceUnavailable.With($"Onyx at {Settings.Address} {what}.");

    private Error Unexpected(string what) => ContextErrors.OnyxUnexpectedReply.With(
        $"Onyx at {Settings.Address} {what} that the pinned {Settings.Version} doesn't send; check the running release against context.sources.onyx.version (spec §8.8).");

    private Error Failed(OnyxCall call, Error error, int? status)
    {
        LogFailed(_parts.Logger, call.Method.Method, call.Route, status, error.Code, error.Message);
        return error;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Onyx call {Method} {Route} failed (HTTP {Status}): {Code} {Reason}")]
    private static partial void LogFailed(ILogger logger, string method, string route, int? status, string code, string reason);
}
