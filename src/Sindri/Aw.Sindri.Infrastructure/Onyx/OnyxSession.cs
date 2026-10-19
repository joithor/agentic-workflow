using System.Globalization;
using System.Net;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>A logged-in Onyx session over HTTP (see <see cref="HttpOnyxProvisioning"/>): each request goes through <see cref="Send"/>, which refuses anything off the allowlist before it is sent.</summary>
/// <param name="client">The session's client, with its cookie.</param>
/// <param name="origin">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="logoutTimeout">How long disposing waits for the logout.</param>
/// <param name="timeout">How long one request waits for Onyx.</param>
internal sealed class OnyxSession(HttpClient client, Uri origin, TimeSpan logoutTimeout, TimeSpan timeout) : IOnyxSession
{
    public ValueTask<Result<bool>> RegisterUser(string email, string password, CancellationToken cancellationToken)
        => throw new NotSupportedException("red");

    public ValueTask<Result<string>> ResetPassword(string email, CancellationToken cancellationToken)
        => throw new NotSupportedException("red");

    public ValueTask<Result<Unit>> MakeAdmin(string email, CancellationToken cancellationToken)
        => throw new NotSupportedException("red");

    public ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken)
        => throw new NotSupportedException("red");

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var deadline = new CancellationTokenSource(logoutTimeout);
            _ = await Send(HttpMethod.Post, "/api/auth/logout", content: null, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // best effort: a slow logout must not decide the outcome (the token is already made); the cookie is dropped with the client either way
        }
        finally
        {
            client.Dispose();
        }
    }

    internal static string Code(HttpStatusCode status) => ((int)status).ToString(CultureInfo.InvariantCulture);

    // The one place a request leaves: the allowlist first, then the send. No password or token reaches an error.
    internal async ValueTask<Result<OnyxAnswer>> Send(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        if (!HttpOnyxProvisioning.Allowed(method, path))
        {
            return SindriErrors.OnyxKeyFailed.With($"{method} {path} is not a request `sindri onyx up` may send to Onyx");
        }

        using var message = new HttpRequestMessage(method, new Uri(origin, path)) { Content = content };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            using var response = await client.SendAsync(message, deadline.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
            return new OnyxAnswer(response.StatusCode, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx didn't answer {method} {path} within {timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s; check `sindri onyx status`, then run `sindri onyx up` again");
        }
        catch (HttpRequestException)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx couldn't be reached for {method} {path}; check `sindri onyx status`, then run `sindri onyx up` again");
        }
    }
}
