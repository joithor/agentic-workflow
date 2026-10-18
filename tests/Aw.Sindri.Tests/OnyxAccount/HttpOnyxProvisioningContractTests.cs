using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// The HTTP adapter over a scripted Onyx that accepts the login, lists one Admin group, makes one key and knows only that key.
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
        public int Logouts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/logout")
            {
                Logouts++;
            }

            var (status, body) = path switch
            {
                "/api/manage/admin/user-group" => (HttpStatusCode.OK, "[{\"id\":3,\"name\":\"Admin\",\"is_default\":true}]"),
                "/api/admin/api-key" => (HttpStatusCode.OK, "{\"api_key\":\"on_made-0123\"}"),
                "/api/me" when request.Headers.Authorization?.Parameter == "on_made-0123" => (HttpStatusCode.OK, "{\"account_type\":\"SERVICE_ACCOUNT\",\"effective_permissions\":[\"basic\",\"admin\"]}"),
                "/api/me" => (HttpStatusCode.Unauthorized, "{}"),
                _ => (HttpStatusCode.OK, "{}"),
            };
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
