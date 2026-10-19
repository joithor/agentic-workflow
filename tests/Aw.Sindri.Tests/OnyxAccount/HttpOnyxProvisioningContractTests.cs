using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// The HTTP adapter over a scripted Onyx that keeps users, passwords, admins and tokens, takes only the right password at login and knows only the tokens that haven't been revoked.
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
        private readonly Dictionary<int, (string Token, string Name, string Email)> _tokens = [];
        private string? _signedInAs;
        private int _nextId = 1;

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

            if (request.Method == HttpMethod.Delete)
            {
                return Revoke(path);
            }

            return path switch
            {
                "/api/auth/login" => Login(body),
                "/api/auth/register" => Register(body),
                "/api/password/reset_password" => Reset(body),
                "/api/manage/admin/users/admin-access" => MakeAdmin(body),
                "/api/manage/admin/deactivate-user" => (HttpStatusCode.NotFound, "{\"detail\":\"User not found\"}"),
                "/api/user/pats" when request.Method == HttpMethod.Get => List(),
                "/api/user/pats" => Mint(body),
                "/api/me" => Me(request.Headers.Authorization?.Parameter),
                _ => (HttpStatusCode.OK, "{}"),
            };
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
            var email = json.RootElement.GetProperty("user_email").GetString()!;
            if (!_users.ContainsKey(email))
            {
                return (HttpStatusCode.NotFound, "{\"detail\":\"User not found\"}");
            }

            _users[email] = "reset-pw-1aA!";
            return (HttpStatusCode.OK, "{\"user_id\":\"u\",\"new_password\":\"reset-pw-1aA!\"}");
        }

        private (HttpStatusCode Status, string Body) MakeAdmin(string body)
        {
            using var json = JsonDocument.Parse(body);
            _ = _admins.Add(json.RootElement.GetProperty("user_email").GetString()!);
            return (HttpStatusCode.OK, "null");
        }

        private (HttpStatusCode Status, string Body) List()
        {
            var mine = _tokens.Where(t => t.Value.Email == _signedInAs).Select(static t => $"{{\"id\":{t.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"name\":\"{t.Value.Name}\"}}");
            return (HttpStatusCode.OK, "[" + string.Join(',', mine) + "]");
        }

        private (HttpStatusCode Status, string Body) Mint(string body)
        {
            using var json = JsonDocument.Parse(body);
            var id = _nextId++;
            var token = $"onyx_pat_made-{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            _tokens[id] = (token, json.RootElement.GetProperty("name").GetString()!, _signedInAs!);   // why: a token is minted only after a login
            return (HttpStatusCode.OK, $"{{\"id\":{id.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"token\":\"{token}\"}}");
        }

        private (HttpStatusCode Status, string Body) Revoke(string path)
        {
            var id = int.Parse(path[(path.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            return _tokens.Remove(id) ? (HttpStatusCode.OK, "{}") : (HttpStatusCode.NotFound, "{}");
        }

        private (HttpStatusCode Status, string Body) Me(string? token)
        {
            var (known, _, email) = _tokens.Values.FirstOrDefault(t => t.Token == token);
            if (known is null)
            {
                return (HttpStatusCode.Unauthorized, "{}");
            }

            var admin = _admins.Contains(email) ? ",\"admin\"" : string.Empty;
            return (HttpStatusCode.OK, $"{{\"account_type\":\"STANDARD\",\"email\":\"{email}\",\"effective_permissions\":[\"basic\"{admin}]}}");
        }
    }
}
