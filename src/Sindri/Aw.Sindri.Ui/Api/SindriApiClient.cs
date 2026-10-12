using System.Globalization;
using System.Net;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The typed client of Sindri's loopback HTTP API (docs/csharp/sindri.md "Command layer and API"). A call that doesn't
/// answer within 2 s is unreachable, like the CLI's; the event stream has no timeout and ends with its connection.
/// </summary>
/// <param name="http">The HTTP client, with no timeout of its own (the app's, or one over a canned handler in tests).</param>
/// <param name="token">The control plane's per-start human token, read at each call (API spec §6).</param>
/// <param name="clock">The clock the 2 s deadline runs on (the app's, so tests drive it with a fake one).</param>
/// <param name="log">The app's log, where an unreadable body's parser detail goes (never the window); none by default.</param>
internal sealed class SindriApiClient(HttpClient http, IHumanTokenSource token, TimeProvider clock, ILogger? log = null) : ISindriApi
{
    private readonly ILogger _log = log ?? NullLogger.Instance;

    /// <summary>
    /// The version of Sindri's API this build speaks (API spec "Versioning": the status reports apiVersion from P1). A
    /// status that reports another one is the Version mismatch state (UI spec §4.1); one that reports none (S1) is not.
    /// </summary>
    internal const int AppApiVersion = 1;

    private static readonly TimeSpan _callTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The app's JSON options (the AwJson profile over the app's context).</summary>
    internal static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(SindriUiJsonContext.Default);

    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken) => Get<PingView>(api, "/sindri/ping", cancellationToken);

    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken) => Get<StatusView>(api, "/sindri/status", cancellationToken);

    public async IAsyncEnumerable<EventView> Events(Uri api, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={after}");
        using var response = await Open(new Uri(api, path), cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            yield break;
        }

        var stream = await Body(response, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            yield break;
        }

        await using (stream.ConfigureAwait(false))
        {
            var items = SseParser.Create(stream).EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (items.ConfigureAwait(false))
            {
                while (await Next(items).ConfigureAwait(false))
                {
                    var e = AwJson.Deserialize<EventView>(System.Text.Encoding.UTF8.GetBytes(items.Current.Data), Json);
                    if (e.IsSuccess)
                    {
                        yield return e.Value;
                    }
                }
            }
        }
    }

    private async Task<Result<T>> Get<T>(Uri api, string path, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_callTimeout, clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var request = await Request(new Uri(api, path), deadline.Token).ConfigureAwait(false);
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            if (Refusal(response.StatusCode, body) is { } refused)
            {
                return refused;
            }

            if (!response.IsSuccessStatusCode)
            {
                return SindriUiErrors.BadAnswer.With(string.Create(CultureInfo.InvariantCulture, $"{path} answered {(int)response.StatusCode}"));
            }

            var value = AwJson.Deserialize<T>(body, Json);
            if (value.IsError)
            {
                // The parser's text names .NET types, JSON paths and byte positions: the log keeps it, the window gets
                // the route in words (UI spec §4.1 Unreadable; as I7 for SND-UI-002).
                SindriApiLog.Unreadable(_log, path, value.Error.Message);
                return SindriUiErrors.BadAnswer.With($"{path} answered with something this build can't read");
            }

            return value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriUiErrors.Unreachable.With($"{api.Authority} didn't answer within 2 s");
        }
        catch (HttpRequestException)
        {
            // The platform's message repeats the address in its own words ("Connection refused (127.0.0.1:61879)"): I7.
            return SindriUiErrors.Unreachable.With($"nothing answered at {api.Authority}");
        }
    }

    // A 403 with Sindri's ProblemDetails is a refusal (API §6: SND-AUTH-001 for a missing or stale token): Sindri's code
    // and its detail as they are (API §3.6), so the window shows Sindri's own sentence and fix. Built like the CLI's
    // reading of a problem (ControlPlaneClient): the code is Sindri's catalogue's, not the app's. Any other non-success,
    // or a 403 without a title, stays SND-UI-003.
    private static Error? Refusal(HttpStatusCode status, byte[] body)
    {
        if (status != HttpStatusCode.Forbidden)
        {
            return null;
        }

        var problem = AwJson.Deserialize<ProblemView>(body, Json);
        return problem is { IsSuccess: true, Value.Title: { Length: > 0 } code }
            ? new Error(code, ErrorKind.Forbidden, problem.Value.Detail is { Length: > 0 } detail ? detail : code)
            : null;
    }

    // Every request says who the app is to the API (UI spec §9.1, API §6): a human, from the UI, with this start's token
    // (read at each call, like the CLI's, so a restarted control plane's new one is sent with no app restart). Never
    // X-Sindri-Terminal (the app is no terminal, so protected actions stay at the CLI) and never Origin (commands refuse
    // it). With no token, or one the platform can't read, the call goes without it and Sindri's SND-AUTH-001 says why.
    // The token goes only into this header: never into an error, the log or the window.
    private async ValueTask<HttpRequestMessage> Request(Uri url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Sindri-Caller", "human");
        request.Headers.Add("X-Sindri-Client", "ui");
        if (await HumanToken(cancellationToken).ConfigureAwait(false) is { } value)
        {
            request.Headers.Add("X-Sindri-Token", value);
        }

        return request;
    }

    private async ValueTask<string?> HumanToken(CancellationToken cancellationToken)
    {
        try
        {
            return await token.Read(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SindriApiLog.TokenUnreadable(_log, token.Location, e.GetType().Name);
            return null;
        }
    }

    // The open event-stream response (owned by the caller), or null when it didn't answer 2xx or nothing answered.
    private async Task<HttpResponseMessage?> Open(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = await Request(url, cancellationToken).ConfigureAwait(false);
            request.Headers.Accept.ParseAdd("text/event-stream");
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            response.Dispose();
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<Stream?> Body(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // A connection that breaks mid-stream ends the stream, like a clean close: the caller reconnects either way.
    private static async ValueTask<bool> Next(IAsyncEnumerator<SseItem<string>> items)
    {
        try
        {
            return await items.MoveNextAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
