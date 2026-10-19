using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// A logged-in Onyx session over HTTP (see <see cref="HttpOnyxProvisioning"/>): each request goes through <see cref="Send"/>, which refuses anything off the allowlist before it is sent.
/// The calls that change a user name no email of their own: they use <see cref="OnyxAccounts.DedicatedEmail"/> (or the retired <see cref="OnyxAccounts.LegacyEmail"/>) inside this type.
/// </summary>
/// <param name="client">The session's client, with its cookie.</param>
/// <param name="origin">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="logoutTimeout">How long disposing waits for the logout.</param>
/// <param name="timeout">How long one request waits for Onyx.</param>
internal sealed partial class OnyxSession(HttpClient client, Uri origin, TimeSpan logoutTimeout, TimeSpan timeout) : IOnyxSession
{
    public async ValueTask<Result<bool>> RetireLegacyUser(CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxUserEmailWire(OnyxAccounts.LegacyEmail));
        var sent = await Send(HttpMethod.Patch, "/api/manage/admin/deactivate-user", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        // 404 "User not found" is the usual answer: an earlier build's user was never made on this Onyx.
        return sent.Value.Status == HttpStatusCode.NotFound ? false : Refusal(sent.Value, "retire the earlier Sindri user") is { } refused ? refused : true;
    }

    public async ValueTask<Result<string?>> ResetDedicatedPassword(CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxUserEmailWire(OnyxAccounts.DedicatedEmail));
        var sent = await Send(HttpMethod.Post, "/api/password/reset_password", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (sent.Value.Status == HttpStatusCode.NotFound)
        {
            return Results.Success<string?>(value: null);   // no such user yet: the caller registers it
        }

        if (Refusal(sent.Value, "reset the password of Sindri's user") is { } refused)
        {
            return refused;
        }

        var reset = AwJson.Deserialize<OnyxResetWire>(Encoding.UTF8.GetBytes(sent.Value.Body), HttpOnyxProvisioning.Json);
        return reset is { IsSuccess: true, Value.NewPassword: { Length: > 0 } fresh }
            ? Results.Success<string?>(fresh)
            : SindriErrors.OnyxKeyFailed.With("Onyx didn't give a new password for Sindri's user; run `sindri onyx up` again");
    }

    public async ValueTask<Result<Unit>> RegisterDedicatedUser(string password, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxRegisterRequestWire(OnyxAccounts.DedicatedEmail, password));
        var sent = await Send(HttpMethod.Post, "/api/auth/register", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        return sent.Value.Success ? Unit.Value : RegisterRefused(sent.Value, password);
    }

    public async ValueTask<Result<Unit>> MakeDedicatedAdmin(CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxAdminAccessWire(OnyxAccounts.DedicatedEmail, IsAdmin: true));
        var sent = await Send(HttpMethod.Patch, "/api/manage/admin/users/admin-access", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        return Refusal(sent.Value, "make Sindri's user an admin") is { } refused ? refused : Unit.Value;
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

    // A refused registration, said in words. Invite-only and a domain list are the usual causes, and both need the owner once; a password Onyx won't take shows Onyx's own reason, never the password.
    private static Error RegisterRefused(OnyxAnswer answer, string password)
    {
        var email = OnyxAccounts.DedicatedEmail;
        if (answer.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx refused to register Sindri's user (HTTP {Code(answer.Status)}): this Onyx is invite-only; invite {email} once in Admin → Users (or turn invite-only off), then run `sindri onyx up` again. After that a rerun doesn't need the invite");
        }

        var reason = new string([.. answer.Body.Replace(password, "…", StringComparison.Ordinal).Where(static c => !char.IsControl(c)).Take(200)]);
        return SindriErrors.OnyxKeyFailed.With($"Onyx refused to register Sindri's user {email} (HTTP {Code(answer.Status)}): {reason}; if Onyx limits email domains (VALID_EMAIL_DOMAINS), allow example.com or make the user by hand (docs/sindri/onyx.md section 5), then run `sindri onyx up` again");
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
