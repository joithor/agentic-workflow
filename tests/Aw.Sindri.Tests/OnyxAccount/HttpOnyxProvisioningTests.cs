using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxAccount;

// The seven setup-only requests (docs/sindri/onyx.md section 7), against a scripted Onyx: their shapes, the refusal of everything else, and what never leaves.
[Lane(TestLane.Integration)]
public sealed class HttpOnyxProvisioningTests
{
    private const string DedicatedEmail = "sindri@sindri.app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string Body, string? Authorization);

    private sealed class Script : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        public Func<Sent, bool> Hang { get; set; } = static _ => false;

        public Func<Sent, bool> Refuse { get; set; } = static _ => false;

        public Func<Sent, (HttpStatusCode Status, string Body)> Answer { get; set; } = static _ => (HttpStatusCode.OK, "{}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new Sent(request.Method, request.RequestUri!.PathAndQuery, body, request.Headers.Authorization?.ToString());
            Requests.Add(sent);
            if (Refuse(sent))
            {
                throw new HttpRequestException("connection refused");
            }

            if (Hang(sent))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            var (status, text) = Answer(sent);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpOnyxProvisioning Adapter(Script script) => new(() => script);

    // A script that answers one path with (status, body) and every other with 200 {}.
    private static Script Answering(string path, HttpStatusCode status, string body)
        => new() { Answer = sent => sent.PathAndQuery == path ? (status, body) : (HttpStatusCode.OK, "{}") };

    private static async Task<IOnyxSession> Session(Script script)
        => (await Adapter(script).SignIn(3000, "a@b.c", "x", Token)).Value;

    [Theory, Trait("Lane", "integration")]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/logout", true)]
    [InlineData("POST", "/api/auth/register", true)]
    [InlineData("POST", "/api/password/reset_password", true)]
    [InlineData("PATCH", "/api/manage/admin/users/admin-access", true)]
    [InlineData("POST", "/api/user/pats", true)]
    [InlineData("GET", "/api/me", true)]
    [InlineData("POST", "/api/admin/api-key", false)]
    [InlineData("GET", "/api/manage/admin/user-group", false)]
    [InlineData("GET", "/api/user/pats", false)]
    [InlineData("DELETE", "/api/user/pats/1", false)]
    [InlineData("PATCH", "/api/manage/admin/users/craft-enabled", false)]
    [InlineData("DELETE", "/api/manage/admin/delete-user", false)]
    [InlineData("PUT", "/api/manage/admin/users", false)]
    [InlineData("POST", "/api/me", false)]
    [InlineData("GET", "/api/auth/login", false)]
    [InlineData("POST", "/api/password/change-password", false)]
    public void Only_the_seven_allowlisted_requests_may_be_sent(string method, string path, bool allowed)
        => Assert.Equal(allowed, HttpOnyxProvisioning.Allowed(new HttpMethod(method), path));

    [Fact, Trait("Lane", "integration")]
    public async Task A_login_posts_a_form_and_the_session_logs_out_when_disposed()
    {
        var script = new Script();

        var session = (await Adapter(script).SignIn(3000, "owner@example.com", "pw value", Token)).Value;
        await session.DisposeAsync();

        var login = script.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("/api/auth/login", login.PathAndQuery);
        Assert.Equal("username=owner%40example.com&password=pw+value", login.Body);
        Assert.Equal("/api/auth/logout", script.Requests[^1].PathAndQuery);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_logout_that_hangs_is_given_up_on_without_throwing_and_the_made_token_survives()
    {
        var script = new Script
        {
            Answer = static sent => sent.PathAndQuery == "/api/user/pats" ? (HttpStatusCode.OK, "{\"token\":\"onyx_pat_abc123\"}") : (HttpStatusCode.OK, "{}"),
            Hang = static sent => sent.PathAndQuery == "/api/auth/logout",
        };
        var adapter = new HttpOnyxProvisioning(() => script, TimeSpan.FromMilliseconds(100));
        var session = (await adapter.SignIn(3000, "a@b.c", "x", Token)).Value;
        var token = await session.CreateToken("sindri-onyx", Token);

        await session.DisposeAsync();   // must not throw

        Assert.Equal("onyx_pat_abc123", token.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refused_login_is_sindri_onyx_008_and_does_not_repeat_the_password()
    {
        var script = new Script { Answer = static _ => (HttpStatusCode.BadRequest, "{\"detail\":\"LOGIN_BAD_CREDENTIALS\"}") };

        var result = await Adapter(script).SignIn(3000, "owner@example.com", "the-password-text", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.DoesNotContain("the-password-text", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_new_user_is_registered_with_a_json_body_and_reported_as_made()
    {
        var script = Answering("/api/auth/register", HttpStatusCode.Created, "{\"id\":\"u1\",\"email\":\"sindri@sindri.app\"}");
        var session = await Session(script);

        var made = await session.RegisterUser(DedicatedEmail, "pw-0123456789", Token);

        Assert.True(made.Value);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/auth/register");
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(DedicatedEmail, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("pw-0123456789", body.RootElement.GetProperty("password").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_email_that_is_already_registered_is_reported_as_not_made_and_not_as_an_error()
    {
        var session = await Session(Answering("/api/auth/register", HttpStatusCode.BadRequest, "{\"detail\":\"REGISTER_USER_ALREADY_EXISTS\"}"));

        var made = await session.RegisterUser(DedicatedEmail, "pw", Token);

        Assert.True(made.IsSuccess);
        Assert.False(made.Value);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"detail\":\"This workspace is invite-only.\"}", "invite-only")]
    [InlineData(HttpStatusCode.Forbidden, "{}", "invite-only")]
    [InlineData(HttpStatusCode.BadRequest, "{\"detail\":{\"code\":\"REGISTER_INVALID_PASSWORD\",\"reason\":\"Password must be 12 characters\"}}", "HTTP 400")]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "HTTP 500")]
    public async Task A_registration_Onyx_refuses_is_sindri_onyx_008_that_says_why_and_never_repeats_the_password(HttpStatusCode status, string body, string says)
    {
        var session = await Session(Answering("/api/auth/register", status, body));

        var made = await session.RegisterUser(DedicatedEmail, "the-password-text", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, made.Error.Code);
        Assert.Contains(says, made.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the-password-text", made.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_reset_asks_for_the_user_by_email_and_returns_the_new_password_only_to_the_caller()
    {
        var script = Answering("/api/password/reset_password", HttpStatusCode.OK, "{\"user_id\":\"u1\",\"new_password\":\"fresh-pass-77\"}");
        var session = await Session(script);

        var reset = await session.ResetPassword(DedicatedEmail, Token);

        Assert.Equal("fresh-pass-77", reset.Value);
        Assert.Equal("{\"user_email\":\"sindri@sindri.app\"}", script.Requests.Single(static r => r.PathAndQuery == "/api/password/reset_password").Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.NotFound, "SND-ONYX-008")]
    [InlineData(HttpStatusCode.InternalServerError, "SND-ONYX-008")]
    public async Task A_reset_Onyx_does_not_do_is_007_for_a_non_admin_and_008_otherwise(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/password/reset_password", status, "{\"detail\":\"x\"}"));

        Assert.Equal(code, (await session.ResetPassword(DedicatedEmail, Token)).Error.Code);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"user_id\":\"u1\",\"new_password\":\"\"}")]
    public async Task A_reset_answer_without_a_password_is_sindri_onyx_008(string body)
    {
        var session = await Session(Answering("/api/password/reset_password", HttpStatusCode.OK, body));

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, (await session.ResetPassword(DedicatedEmail, Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Making_a_user_an_admin_patches_the_admin_access_route()
    {
        var script = Answering("/api/manage/admin/users/admin-access", HttpStatusCode.OK, "null");
        var session = await Session(script);

        var made = await session.MakeAdmin(DedicatedEmail, Token);

        Assert.True(made.IsSuccess);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/manage/admin/users/admin-access");
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("{\"user_email\":\"sindri@sindri.app\",\"is_admin\":true}", request.Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.NotFound, "SND-ONYX-008")]
    [InlineData(HttpStatusCode.PaymentRequired, "SND-ONYX-008")]
    public async Task A_login_that_may_not_make_admins_is_007_and_any_other_refusal_is_008(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/manage/admin/users/admin-access", status, "{}"));

        Assert.Equal(code, (await session.MakeAdmin(DedicatedEmail, Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_token_is_made_unscoped_and_without_expiry_and_comes_back_once()
    {
        var script = Answering("/api/user/pats", HttpStatusCode.OK, "{\"id\":4,\"name\":\"sindri-onyx\",\"token\":\"onyx_pat_abc123\",\"token_display\":\"onyx_pat_abc1****x9\",\"scopes\":null}");
        var session = await Session(script);

        var token = await session.CreateToken("sindri-onyx", Token);

        Assert.Equal("onyx_pat_abc123", token.Value);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/user/pats");
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("sindri-onyx", body.RootElement.GetProperty("name").GetString());
        Assert.False(body.RootElement.TryGetProperty("scopes", out var scopes) && scopes.ValueKind != JsonValueKind.Null);
        Assert.False(body.RootElement.TryGetProperty("expiration_days", out var days) && days.ValueKind != JsonValueKind.Null);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-ONYX-007")]
    [InlineData(HttpStatusCode.InternalServerError, "SND-ONYX-008")]
    public async Task A_token_Onyx_refuses_is_007_for_a_login_without_the_permission_and_008_otherwise(HttpStatusCode status, string code)
    {
        var session = await Session(Answering("/api/user/pats", status, "{}"));

        Assert.Equal(code, (await session.CreateToken("k", Token)).Error.Code);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"token\":\"\"}")]
    [InlineData("{}")]
    public async Task A_token_answer_without_a_token_is_sindri_onyx_008(string body)
    {
        var session = await Session(Answering("/api/user/pats", HttpStatusCode.OK, body));

        var token = await session.CreateToken("k", Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, token.Error.Code);
        Assert.Contains("didn't make the token", token.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_token_is_the_dedicated_admin_only_for_that_users_email_with_the_admin_permission()
    {
        var accounts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bearer good"] = "{\"account_type\":\"STANDARD\",\"email\":\"sindri@sindri.app\",\"effective_permissions\":[\"basic\",\"admin\"]}",
            ["Bearer basic"] = "{\"account_type\":\"STANDARD\",\"email\":\"sindri@sindri.app\",\"effective_permissions\":[\"basic\"]}",
            ["Bearer human"] = "{\"account_type\":\"STANDARD\",\"email\":\"someone@example.com\",\"effective_permissions\":[\"admin\"]}",
            ["Bearer noemail"] = "{\"account_type\":\"STANDARD\",\"effective_permissions\":[\"admin\"]}",
        };
        var script = new Script { Answer = sent => accounts.TryGetValue(sent.Authorization ?? string.Empty, out var body) ? (HttpStatusCode.OK, body) : (HttpStatusCode.Unauthorized, "{}") };
        var adapter = Adapter(script);

        Assert.True((await adapter.TokenIsAdmin(3000, "good", DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "basic", DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "human", DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "noemail", DedicatedEmail, Token)).Value);
        Assert.False((await adapter.TokenIsAdmin(3000, "stranger", DedicatedEmail, Token)).Value);
        Assert.All(script.Requests, static r => Assert.Equal("/api/me", r.PathAndQuery));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_request_Onyx_does_not_answer_in_time_names_the_wait()
    {
        var script = new Script { Hang = static sent => sent.PathAndQuery == "/api/me" };
        var adapter = new HttpOnyxProvisioning(() => script, requestTimeout: TimeSpan.FromMilliseconds(100));

        var result = await adapter.TokenIsAdmin(3000, "k", DedicatedEmail, Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("didn't answer GET /api/me", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_Onyx_that_cannot_be_reached_is_an_error_not_a_false()
    {
        var script = new Script { Refuse = static _ => true };

        var admin = await Adapter(script).TokenIsAdmin(3000, "k", DedicatedEmail, Token);
        var login = await Adapter(script).SignIn(3000, "a@b.c", "x", Token);

        Assert.Contains("couldn't be reached", admin.Error.Message, StringComparison.Ordinal);
        Assert.Contains("couldn't be reached", login.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Requests_go_to_loopback_only()
    {
        var script = new Script();
        var uris = new List<Uri>();
        var recording = new HttpOnyxProvisioning(() => new Capture(uris, script));

        _ = await recording.TokenIsAdmin(3456, "k", DedicatedEmail, Token);

        Assert.Equal(new Uri("http://127.0.0.1:3456/api/me"), Assert.Single(uris));
    }

    private sealed class Capture(List<Uri> uris, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            uris.Add(request.RequestUri!);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
