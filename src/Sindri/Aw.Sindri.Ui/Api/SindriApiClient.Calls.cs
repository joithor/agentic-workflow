using System.Globalization;
using System.Net;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>One call to Sindri's API (<see cref="SindriApiClient"/>, continued): the headers, the deadline and the answer.</summary>
internal sealed partial class SindriApiClient
{
    // The sequence of the last command the API accepted, sent once as X-Sindri-After on the next query (API §2.4,
    // UI §9.1); 0 when there is none to send.
    private long _after;

    private Task<Result<T>> Get<T>(Uri api, string path, CancellationToken cancellationToken) =>
        Call<T>(api, HttpMethod.Get, path, cancellationToken);

    private async Task<Result<T>> Call<T>(Uri api, HttpMethod method, string path, CancellationToken cancellationToken, byte[]? body = null)
    {
        using var timeout = new CancellationTokenSource(_callTimeout, clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var request = await Request(method, new Uri(api, path), query: method == HttpMethod.Get, deadline.Token).ConfigureAwait(false);
            if (body is not null)
            {
                request.Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } };
            }

            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var answer = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? Read<T>(path, answer) : Refused<T>(path, response.StatusCode, answer);
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

    private Result<T> Read<T>(string path, byte[] body)
    {
        var value = AwJson.Deserialize<T>(body, Json);
        if (value.IsError)
        {
            // The parser's text names .NET types, JSON paths and byte positions: the log keeps it, the window gets the
            // route in words (UI spec §4.1 Unreadable; as I7 for SND-UI-002).
            SindriApiLog.Unreadable(_log, path, value.Error.Message);
            return SindriUiErrors.BadAnswer.With($"{path} answered with something this build can't read");
        }

        // An accepted command (CreateItem nests it) is waited for by the next query.
        if ((object?)value.Value switch { CommandAcceptedView a => a.Sequence, ItemCreatedView c => c.Accepted.Sequence, _ => (long?)null } is { } sequence)
        {
            _ = Interlocked.Exchange(ref _after, sequence);
        }

        return value;
    }

    // A refusal the API explains (ProblemDetails: title = the SND code, detail = the message with its fix) is that
    // error, shown as served (UI spec §4.1 Error); any other non-2xx is an answer this build can't read.
    private static Result<T> Refused<T>(string path, HttpStatusCode status, byte[] body)
    {
        var problem = AwJson.Deserialize<ProblemView>(body, Json);
        return problem.IsSuccess && problem.Value.Title.StartsWith("SND-", StringComparison.Ordinal)
            ? new Error(problem.Value.Title, KindOf(status), problem.Value.Detail)
            : SindriUiErrors.BadAnswer.With(string.Create(CultureInfo.InvariantCulture, $"{path} answered {(int)status}"));
    }

    private static ErrorKind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 => ErrorKind.Invalid,
        403 => ErrorKind.Forbidden,
        404 => ErrorKind.NotFound,
        409 => ErrorKind.Conflict,
        429 => ErrorKind.RateLimited,
        504 => ErrorKind.Timeout,
        _ => ErrorKind.Unavailable,
    };

    // Every request says who the app is to the API (UI spec §9.1, API §6): a human, from the UI, with this start's
    // token. Never X-Sindri-Terminal (the app is no terminal, so protected actions stay at the CLI) and never Origin
    // (commands refuse it). The first query after an accepted command carries its sequence as X-Sindri-After; the event
    // stream and commands never do.
    private async Task<HttpRequestMessage> Request(HttpMethod method, Uri url, bool query, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Sindri-Caller", "human");
        request.Headers.Add("X-Sindri-Client", "ui");
        if (await Token(cancellationToken).ConfigureAwait(false) is { } token)
        {
            request.Headers.Add("X-Sindri-Token", token);
        }

        if (query && Interlocked.Exchange(ref _after, 0) is var after and > 0)
        {
            request.Headers.Add("X-Sindri-After", after.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    // The token as the platform reads it now; none when it can't be read, so the API answers SND-AUTH-001 and the app shows it.
    private async Task<string?> Token(CancellationToken cancellationToken)
    {
        try
        {
            return await tokens.Read(cancellationToken).ConfigureAwait(false) is { Length: > 0 } token ? token : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
