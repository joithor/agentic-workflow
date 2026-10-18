using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxAccount;

// The five setup-only requests of spec §5.2, against a scripted Onyx: their shapes, the refusal of everything else, and what never leaves.
[Lane(TestLane.Integration)]
public sealed class HttpOnyxProvisioningTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string Body, string? Authorization);

    private sealed class Script : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        public Func<Sent, bool> Hang { get; set; } = static _ => false;

        public Func<Sent, (HttpStatusCode Status, string Body)> Answer { get; set; } = static _ => (HttpStatusCode.OK, "{}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new Sent(request.Method, request.RequestUri!.PathAndQuery, body, request.Headers.Authorization?.ToString());
            Requests.Add(sent);
            if (Hang(sent))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            var (status, text) = Answer(sent);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpOnyxProvisioning Adapter(Script script) => new(() => script);

    [Theory, Trait("Lane", "integration")]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/logout", true)]
    [InlineData("GET", "/api/manage/admin/user-group", true)]
    [InlineData("POST", "/api/admin/api-key", true)]
    [InlineData("GET", "/api/me", true)]
    [InlineData("POST", "/api/auth/register", false)]
    [InlineData("GET", "/api/admin/api-key", false)]
    [InlineData("DELETE", "/api/admin/api-key", false)]
    [InlineData("GET", "/api/manage/users", false)]
    [InlineData("POST", "/api/me", false)]
    [InlineData("GET", "/api/auth/login", false)]
    public void Only_the_five_allowlisted_requests_may_be_sent(string method, string path, bool allowed)
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
    public async Task A_logout_that_hangs_is_given_up_on_without_throwing_and_the_made_key_survives()
    {
        var script = new Script
        {
            Answer = static sent => sent.PathAndQuery == "/api/admin/api-key" ? (HttpStatusCode.OK, "{\"api_key\":\"on_abc123\"}") : (HttpStatusCode.OK, "{}"),
            Hang = static sent => sent.PathAndQuery == "/api/auth/logout",
        };
        var adapter = new HttpOnyxProvisioning(() => script, TimeSpan.FromMilliseconds(100));
        var session = (await adapter.SignIn(3000, "a@b.c", "x", Token)).Value;
        var key = await session.CreateKey("sindri-onyx", 3, Token);

        await session.DisposeAsync();   // must not throw

        Assert.Equal("on_abc123", key.Value);
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
    public async Task The_admin_group_is_the_default_group_named_Admin()
    {
        var script = new Script
        {
            Answer = static sent => sent.PathAndQuery.StartsWith("/api/manage/admin/user-group", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, "[{\"id\":7,\"name\":\"Basic\",\"is_default\":true},{\"id\":3,\"name\":\"Admin\",\"is_default\":true},{\"id\":9,\"name\":\"Admin\",\"is_default\":false}]")
                : (HttpStatusCode.OK, "{}"),
        };
        var session = (await Adapter(script).SignIn(3000, "a@b.c", "x", Token)).Value;

        var group = await session.AdminGroup(Token);

        Assert.Equal(3, group.Value);
        Assert.Equal("/api/manage/admin/user-group?include_default=true", script.Requests[1].PathAndQuery);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task A_login_that_may_not_list_groups_is_sindri_onyx_007(HttpStatusCode status)
    {
        var script = new Script { Answer = sent => sent.PathAndQuery.Contains("user-group", StringComparison.Ordinal) ? (status, "{}") : (HttpStatusCode.OK, "{}") };
        var session = (await Adapter(script).SignIn(3000, "a@b.c", "x", Token)).Value;

        Assert.Equal(SindriErrors.OnyxNotAdmin.Code, (await session.AdminGroup(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_key_is_made_with_a_name_and_the_admin_group_and_comes_back_once()
    {
        var script = new Script { Answer = static sent => sent.PathAndQuery == "/api/admin/api-key" ? (HttpStatusCode.OK, "{\"api_key\":\"on_abc123\"}") : (HttpStatusCode.OK, "{}") };
        var session = (await Adapter(script).SignIn(3000, "a@b.c", "x", Token)).Value;

        var key = await session.CreateKey("sindri-onyx", 3, Token);

        Assert.Equal("on_abc123", key.Value);
        var request = script.Requests.Single(static r => r.PathAndQuery == "/api/admin/api-key");
        Assert.Equal("{\"name\":\"sindri-onyx\",\"group_ids\":[3]}", request.Body);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_key_is_an_admin_only_as_a_service_account_with_the_admin_permission()
    {
        var accounts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bearer good"] = "{\"account_type\":\"SERVICE_ACCOUNT\",\"effective_permissions\":[\"basic\",\"admin\"]}",
            ["Bearer basic"] = "{\"account_type\":\"SERVICE_ACCOUNT\",\"effective_permissions\":[\"basic\"]}",
            ["Bearer human"] = "{\"account_type\":\"STANDARD\",\"effective_permissions\":[\"admin\"]}",
        };
        var script = new Script { Answer = sent => accounts.TryGetValue(sent.Authorization ?? string.Empty, out var body) ? (HttpStatusCode.OK, body) : (HttpStatusCode.Unauthorized, "{}") };
        var adapter = Adapter(script);

        Assert.True((await adapter.KeyIsAdmin(3000, "good", Token)).Value);
        Assert.False((await adapter.KeyIsAdmin(3000, "basic", Token)).Value);
        Assert.False((await adapter.KeyIsAdmin(3000, "human", Token)).Value);
        Assert.False((await adapter.KeyIsAdmin(3000, "stranger", Token)).Value);
        Assert.All(script.Requests, static r => Assert.Equal("/api/me", r.PathAndQuery));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Requests_go_to_loopback_only()
    {
        var script = new Script();
        var uris = new List<Uri>();
        var recording = new HttpOnyxProvisioning(() => new Capture(uris, script));

        _ = await recording.KeyIsAdmin(3456, "k", Token);

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
