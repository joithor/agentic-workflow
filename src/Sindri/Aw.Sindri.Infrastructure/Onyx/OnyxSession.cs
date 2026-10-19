using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
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
    public async ValueTask<Result<bool>> RegisterUser(string email, string password, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxRegisterRequestWire(email, password));
        var sent = await Send(HttpMethod.Post, "/api/auth/register", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        var answer = sent.Value;
        if (answer.Success)
        {
            return true;
        }

        // fastapi-users answers 400 REGISTER_USER_ALREADY_EXISTS for a taken email: that is not a failure, the user is there.
        return answer.Body.Contains("ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) ? false : RegisterRefused(answer, email, password);
    }

    public async ValueTask<Result<string>> ResetPassword(string email, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxUserEmailWire(email));
        var sent = await Send(HttpMethod.Post, "/api/password/reset_password", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (Refusal(sent.Value, $"reset the password of {email}") is { } refused)
        {
            return refused;
        }

        var reset = AwJson.Deserialize<OnyxResetWire>(Encoding.UTF8.GetBytes(sent.Value.Body), HttpOnyxProvisioning.Json);
        return reset is { IsSuccess: true, Value.NewPassword: { Length: > 0 } fresh }
            ? fresh
            : SindriErrors.OnyxKeyFailed.With("Onyx didn't give a new password for Sindri's user; run `sindri onyx up` again");
    }

    public async ValueTask<Result<Unit>> MakeAdmin(string email, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxAdminAccessWire(email, IsAdmin: true));
        var sent = await Send(HttpMethod.Patch, "/api/manage/admin/users/admin-access", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        return Refusal(sent.Value, $"make {email} an admin") is { } refused ? refused : Unit.Value;
    }

    public async ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxPatRequestWire(name, ExpirationDays: null, Scopes: null));
        var sent = await Send(HttpMethod.Post, "/api/user/pats", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (sent.Value.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return NotAdmin(sent.Value.Status);
        }

        var made = sent.Value.Success ? AwJson.Deserialize<OnyxPatWire>(Encoding.UTF8.GetBytes(sent.Value.Body), HttpOnyxProvisioning.Json) : null;
        return made is { IsSuccess: true, Value.Token: { Length: > 0 } raw }
            ? raw
            : SindriErrors.OnyxKeyFailed.With($"Onyx didn't make the token (HTTP {Code(sent.Value.Status)}); run `sindri onyx up` again");
    }

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

    private static ByteArrayContent JsonBody<T>(T value)
    {
        var body = new ByteArrayContent(AwJson.Serialize(value, HttpOnyxProvisioning.Json));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return body;
    }

    // The error of an admin route's answer, or null when it succeeded: 401 and 403 mean the login isn't an admin (SND-ONYX-007), anything else is SND-ONYX-008.
    private static Error? Refusal(OnyxAnswer answer, string what)
    {
        if (answer.Success)
        {
            return null;
        }

        if (answer.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return NotAdmin(answer.Status);
        }

        var missing = answer.Status == HttpStatusCode.NotFound ? ": no such user" : string.Empty;
        return SindriErrors.OnyxKeyFailed.With($"Onyx refused to {what} (HTTP {Code(answer.Status)}){missing}; run `sindri onyx up` again");
    }

    private static Error NotAdmin(HttpStatusCode status) => SindriErrors.OnyxNotAdmin.With(
        $"Onyx refused (HTTP {Code(status)}): that login isn't an admin; the first account created at the sign-up page is the admin, so log in as it, or make this account an admin in Onyx's admin users page, then run `sindri onyx up` again");

    // A refused registration, said in words: invite-only is the usual cause; otherwise Onyx's own reason (a password it won't take), with the password itself never shown.
    private static Error RegisterRefused(OnyxAnswer answer, string email, string password)
    {
        if (answer.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx refused to register Sindri's user (HTTP {Code(answer.Status)}): this Onyx is invite-only; invite {email} in Admin → Users (or turn invite-only off), then run `sindri onyx up` again");
        }

        var reason = new string([.. answer.Body.Replace(password, "…", StringComparison.Ordinal).Where(static c => !char.IsControl(c)).Take(200)]);
        return SindriErrors.OnyxKeyFailed.With($"Onyx refused to register Sindri's user {email} (HTTP {Code(answer.Status)}): {reason}; fix what it says, then run `sindri onyx up` again");
    }

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
