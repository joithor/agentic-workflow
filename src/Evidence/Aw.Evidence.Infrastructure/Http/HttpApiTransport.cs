using System.Net;
using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Http;

/// <summary>Sends one request over HttpClient with redirects switched off, so the service decides whether a redirect is followed.</summary>
internal sealed class HttpApiTransport : IApiTransport, IDisposable
{
    private readonly HttpClient _client;

    public HttpApiTransport(TimeSpan timeout)
    {
        _client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = timeout };
    }

    /// <inheritdoc/>
    public async ValueTask<Result<HttpReply>> SendAsync(HttpCall call, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(new HttpMethod(call.Method.ToString().ToUpperInvariant()), call.Url);
        if (call.Body is not null)
        {
            message.Content = new StringContent(call.Body, Encoding.UTF8, "application/json");
        }

        foreach (var (name, value) in call.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                _ = message.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        try
        {
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpReply((int)response.StatusCode, response.Headers.Location?.OriginalString, body);
        }
        catch (HttpRequestException ex)
        {
            return EvidenceErrors.RequestFailed.With($"{message.Method} {call.Url} failed: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EvidenceErrors.RequestFailed.With($"{message.Method} {call.Url} timed out");
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();
}
