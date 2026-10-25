using Aw.Kernel.Services;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/code-index/status?repo=: each repo's index state for the caller's repos (a session sees only its own).</summary>
internal static class GetCodeIndexStatusEndpoint
{
    internal static RouteGroupBuilder MapGetCodeIndexStatus(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/code-index/status", static async (string? repo, IService<GetCodeIndexStatusRequest, CodeIndexStatusView> status, CancellationToken ct) =>
        {
            var answer = await status.Handle(new GetCodeIndexStatusRequest(repo), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
