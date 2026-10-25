using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The setup writes of the fake handler (setup-once spec §7.2): the repo, the agents' registration and Onyx's three calls.</summary>
internal sealed partial class FakeSindriHandler
{
    private static readonly string[] _setupWrites = [SetupRoutes.Repos, SetupRoutes.Mcp, SetupRoutes.OnyxSetup, SetupRoutes.OnyxAccount, SetupRoutes.OnyxDefer];

    private static bool IsSetupWrite(string path) => _setupWrites.Contains(path, StringComparer.Ordinal);

    private async Task<HttpResponseMessage> SetupWrite(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return path switch
        {
            SetupRoutes.Repos => Answer(Counted(await server.AddRepo(uri, await Body<AddRepoRequest>(request, cancellationToken), cancellationToken), server.AddRepoAnswer)),
            SetupRoutes.Mcp => Answer(Counted(await server.RegisterMcp(uri, await Body<RegisterMcpRequest>(request, cancellationToken), cancellationToken), server.RegisterMcpAnswer)),
            SetupRoutes.OnyxSetup => Answer(Counted(await server.SetupOnyx(uri, cancellationToken), server.SetupOnyxAnswer)),
            SetupRoutes.OnyxAccount => Answer(Counted(await server.ConnectOnyx(uri, await Body<OnyxLogin>(request, cancellationToken), cancellationToken), server.ConnectOnyxAnswer)),
            _ => Answer(Counted(await server.DeferOnyx(uri, await Body<OnyxDeferRequest>(request, cancellationToken), cancellationToken), server.DeferOnyxAnswer)),
        };
    }
}
