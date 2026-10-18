using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxProvisioning over HTTP to http://127.0.0.1:&lt;port&gt; only (spec §5.2). Five requests are allowed and nothing else; one that isn't on the list is refused before anything is sent (<see cref="Allowed"/>).
/// The login cookie lives in the session's own container and goes with the session; the password and the key are never put in an error message or a log. Not used by the query adapter, and it does not use it.
/// The role step (spec §5.2, "the builder finds"): v4.9.0's `APIKeyArgs` is `{name, group_ids}`; a key's service account gets the admin role by joining the default "Admin" group
/// (`backend/onyx/db/api_key.py` `insert_api_key` → `set_user_groups__no_commit`; `backend/onyx/server/api_key/api.py:52` allows the group list to be uncapped), so the group id comes from the user-group list.
/// </summary>
internal sealed class HttpOnyxProvisioning : IOnyxProvisioning
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(OnyxHealthJsonContext.Default);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    private static readonly (HttpMethod Method, string Path)[] _allowed =
    [
        (HttpMethod.Post, "/api/auth/login"),
        (HttpMethod.Post, "/api/auth/logout"),
        (HttpMethod.Get, "/api/manage/admin/user-group"),
        (HttpMethod.Post, "/api/admin/api-key"),
        (HttpMethod.Get, "/api/me"),
    ];

    private static readonly TimeSpan _logoutTimeout = TimeSpan.FromSeconds(5);

    private readonly Func<HttpMessageHandler> _handler;
    private readonly TimeSpan _logout;

    /// <summary>Initializes the adapter for production: a handler per session, no redirect, no proxy.</summary>
    public HttpOnyxProvisioning()
        : this(static () => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = true }, _logoutTimeout)
    {
    }

    /// <summary>Initializes the adapter over a handler factory (a test hands one pointing at a fake server).</summary>
    /// <param name="handler">Makes the handler a session's client uses; it must keep cookies.</param>
    /// <param name="logoutTimeout">How long disposing a session waits for the logout; null takes 5 s.</param>
    internal HttpOnyxProvisioning(Func<HttpMessageHandler> handler, TimeSpan? logoutTimeout = null)
    {
        _handler = handler;
        _logout = logoutTimeout ?? _logoutTimeout;
    }

    /// <summary>Whether a request is on the provisioning allowlist: the method and the path (no query), exactly.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The absolute path.</param>
    /// <returns>True when it may be sent.</returns>
    internal static bool Allowed(HttpMethod method, string path) => _allowed.Any(a => a.Method == method && string.Equals(a.Path, path, StringComparison.Ordinal));

    /// <inheritdoc />
#pragma warning disable CA2000 // the session owns its client and disposes both when the caller disposes the session
    public async ValueTask<Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken)
    {
        var session = new Session(NewClient(), Origin(port), _logout);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["username"] = email, ["password"] = password });
        var sent = await session.Send(HttpMethod.Post, "/api/auth/login", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError || !sent.Value.Success)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return sent.IsError
                ? sent.Error
                : SindriErrors.OnyxKeyFailed.With($"Onyx refused that login (HTTP {Code(sent.Value.Status)}); check the email and password of the account you created at the sign-up page, then run `sindri onyx up` again");
        }

        return Results.Success<IOnyxSession>(session);
    }

#pragma warning restore CA2000

    /// <inheritdoc />
    public async ValueTask<Result<bool>> KeyIsAdmin(int port, string key, CancellationToken cancellationToken)
    {
        using var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
#pragma warning disable CA2000 // a Session only borrows the client, which the using above disposes
        var session = new Session(client, Origin(port), _logout);
#pragma warning restore CA2000
        var sent = await session.Send(HttpMethod.Get, "/api/me", content: null, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (!sent.Value.Success)
        {
            return false;   // refused: not a key Onyx knows
        }

        var me = AwJson.Deserialize<OnyxMeWire>(Encoding.UTF8.GetBytes(sent.Value.Body), _json);
        return me is { IsSuccess: true, Value: { } wire }
            && string.Equals(wire.AccountType, "SERVICE_ACCOUNT", StringComparison.Ordinal)
            && wire.EffectivePermissions?.Contains("admin", StringComparer.Ordinal) == true;
    }

    private HttpClient NewClient() => new(_handler(), disposeHandler: true) { Timeout = _timeout + TimeSpan.FromSeconds(5) };

    private static Uri Origin(int port) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"));

    private static string Code(HttpStatusCode status) => ((int)status).ToString(CultureInfo.InvariantCulture);

    private sealed record Answer(HttpStatusCode Status, string Body)
    {
        public bool Success => (int)Status is >= 200 and < 300;
    }

    private sealed class Session(HttpClient client, Uri origin, TimeSpan logoutTimeout) : IOnyxSession
    {
        public async ValueTask<Result<int>> AdminGroup(CancellationToken cancellationToken)
        {
            var sent = await Send(HttpMethod.Get, "/api/manage/admin/user-group", content: null, cancellationToken, query: "?include_default=true").ConfigureAwait(false);
            if (sent.IsError)
            {
                return sent.Error;
            }

            if (sent.Value.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                return NotAdmin();
            }

            var groups = sent.Value.Success ? AwJson.Deserialize<OnyxGroupWire[]>(Encoding.UTF8.GetBytes(sent.Value.Body), _json) : null;
            var admin = groups is { IsSuccess: true } read ? read.Value.FirstOrDefault(static g => g.IsDefault && string.Equals(g.Name, "Admin", StringComparison.Ordinal)) : null;
            return admin is null
                ? SindriErrors.OnyxKeyFailed.With($"Onyx's group list gave no default Admin group (HTTP {Code(sent.Value.Status)}); this Onyx release may differ from the pinned one")
                : admin.Id;
        }

        public async ValueTask<Result<string>> CreateKey(string name, int groupId, CancellationToken cancellationToken)
        {
            var request = new OnyxKeyRequestWire(name, [groupId]);
            using var body = new ByteArrayContent(AwJson.Serialize(request, _json));
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var sent = await Send(HttpMethod.Post, "/api/admin/api-key", body, cancellationToken).ConfigureAwait(false);
            if (sent.IsError)
            {
                return sent.Error;
            }

            if (sent.Value.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                return NotAdmin();
            }

            var key = sent.Value.Success ? AwJson.Deserialize<OnyxKeyWire>(Encoding.UTF8.GetBytes(sent.Value.Body), _json) : null;
            return key is { IsSuccess: true, Value.ApiKey: { Length: > 0 } raw }
                ? raw
                : SindriErrors.OnyxKeyFailed.With($"Onyx didn't make the key (HTTP {Code(sent.Value.Status)}); run `sindri onyx up` again");
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
                // best effort: a slow logout must not decide the outcome (the key is already made); the cookie is dropped with the client either way
            }
            finally
            {
                client.Dispose();
            }
        }

        // The one place a request leaves: the allowlist first, then the send. Neither the password nor the key reaches an error.
        internal async ValueTask<Result<Answer>> Send(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken, string query = "")
        {
            if (!Allowed(method, path))
            {
                return SindriErrors.OnyxKeyFailed.With($"{method} {path} is not a request `sindri onyx up` may send to Onyx");
            }

            using var message = new HttpRequestMessage(method, new Uri(origin, path + query)) { Content = content };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_timeout);
            try
            {
                using var response = await client.SendAsync(message, deadline.Token).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
                return new Answer(response.StatusCode, text);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SindriErrors.OnyxKeyFailed.With($"Onyx didn't answer {method} {path} within {_timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; check `sindri onyx status`, then run `sindri onyx up` again");
            }
            catch (HttpRequestException)
            {
                return SindriErrors.OnyxKeyFailed.With($"Onyx couldn't be reached for {method} {path}; check `sindri onyx status`, then run `sindri onyx up` again");
            }
        }

        private static Error NotAdmin() => SindriErrors.OnyxNotAdmin.With(
            "that Onyx login isn't an admin; the first account created at the sign-up page is the admin, so log in as it, or make this account an admin in Onyx's admin users page, then run `sindri onyx up` again");
    }
}
