using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The typed client of Sindri's loopback HTTP API (docs/csharp/sindri.md "Command layer and API"). A call that doesn't
/// answer within 2 s is unreachable, like the CLI's; the event stream has no timeout and ends with its connection.
/// </summary>
/// <param name="http">The HTTP client, with no timeout of its own (the app's, or one over a canned handler in tests).</param>
/// <param name="clock">The clock the 2 s deadline runs on (the app's, so tests drive it with a fake one).</param>
internal sealed class SindriApiClient(HttpClient http, TimeProvider clock) : ISindriApi
{

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
            using var request = Request(new Uri(api, path));
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return SindriUiErrors.BadAnswer.With(string.Create(CultureInfo.InvariantCulture, $"{path} answered {(int)response.StatusCode}"));
            }

            var value = AwJson.Deserialize<T>(body, Json);
            return value.IsError ? SindriUiErrors.BadAnswer.With($"{path}: {value.Error.Message}") : value;
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

    // Every request says who the app is to the API (UI spec §9.1, API §6): a human, from the UI. Never X-Sindri-Terminal
    // (the app is no terminal, so protected actions stay at the CLI) and never Origin (commands refuse it).
    private static HttpRequestMessage Request(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Sindri-Caller", "human");
        request.Headers.Add("X-Sindri-Client", "ui");
        return request;
    }

    // The open event-stream response (owned by the caller), or null when it didn't answer 2xx or nothing answered.
    private async Task<HttpResponseMessage?> Open(Uri url, CancellationToken cancellationToken)
    {
        using var request = Request(url);
        request.Headers.Accept.ParseAdd("text/event-stream");
        try
        {
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
