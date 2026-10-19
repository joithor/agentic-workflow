using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// The HTTP adapter over a scripted Onyx that keeps users, passwords and admins, takes only the right password at login and knows only the token it made.
[Lane(TestLane.Integration)]
public sealed class HttpOnyxProvisioningContractTests : OnyxProvisioningContract
{
    protected override (IOnyxProvisioning Provisioning, Func<int> Logouts) Create()
    {
        var onyx = new ScriptedOnyx();
        return (new HttpOnyxProvisioning(() => onyx), () => onyx.Logouts);
    }

    private sealed class ScriptedOnyx : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _users = new(StringComparer.Ordinal);
        private readonly HashSet<string> _admins = new(StringComparer.Ordinal);
        private string? _signedInAs;

        public int Logouts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var (status, answer) = Handle(request, body);
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        private (HttpStatusCode Status, string Body) Handle(HttpRequestMessage request, string body)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/logout")
            {
                Logouts++;
            }

            return path switch
            {
                "/api/auth/login" => Login(body),
                "/api/auth/register" => Register(body),
                "/api/password/reset_password" => Reset(body),
                "/api/manage/admin/users/admin-access" => MakeAdmin(body),
                "/api/user/pats" => (HttpStatusCode.OK, "{\"token\":\"onyx_pat_made-0123\"}"),
                "/api/me" => Me(request.Headers.Authorization?.Parameter),
                _ => (HttpStatusCode.OK, "{}"),
            };
        }

        private (HttpStatusCode Status, string Body) MakeAdmin(string body)
        {
            using var json = JsonDocument.Parse(body);
            _ = _admins.Add(json.RootElement.GetProperty("user_email").GetString()!);
            return (HttpStatusCode.OK, "null");
        }

        private (HttpStatusCode Status, string Body) Me(string? token)
        {
            if (token != "onyx_pat_made-0123" || _signedInAs is not { } who)
            {
                return (HttpStatusCode.Unauthorized, "{}");
            }

            var admin = _admins.Contains(who) ? ",\"admin\"" : string.Empty;
            return (HttpStatusCode.OK, $"{{\"account_type\":\"STANDARD\",\"email\":\"{who}\",\"effective_permissions\":[\"basic\"{admin}]}}");
        }

        private (HttpStatusCode Status, string Body) Login(string body)
        {
            var form = System.Web.HttpUtility.ParseQueryString(body);
            var name = form["username"] ?? string.Empty;
            _signedInAs = name;
            return !_users.TryGetValue(name, out var current) || string.Equals(current, form["password"], StringComparison.Ordinal)
                ? (HttpStatusCode.OK, "{}")
                : (HttpStatusCode.BadRequest, "{}");
        }

        private (HttpStatusCode Status, string Body) Register(string body)
        {
            using var json = JsonDocument.Parse(body);
            var email = json.RootElement.GetProperty("email").GetString()!;
            return _users.TryAdd(email, json.RootElement.GetProperty("password").GetString()!)
                ? (HttpStatusCode.Created, "{}")
                : (HttpStatusCode.BadRequest, "{\"detail\":\"REGISTER_USER_ALREADY_EXISTS\"}");
        }

        private (HttpStatusCode Status, string Body) Reset(string body)
        {
            using var json = JsonDocument.Parse(body);
            _users[json.RootElement.GetProperty("user_email").GetString()!] = "reset-pw-1aA!";
            return (HttpStatusCode.OK, "{\"user_id\":\"u\",\"new_password\":\"reset-pw-1aA!\"}");
        }
    }
}
