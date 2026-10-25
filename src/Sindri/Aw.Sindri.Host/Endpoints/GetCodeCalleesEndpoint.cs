using Aw.Kernel.Services;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/code-index/callees?ref=&amp;depth=&amp;repo=: the edges out of a symbol, same shape as callers.</summary>
internal static class GetCodeCalleesEndpoint
{
    internal static RouteGroupBuilder MapGetCodeCallees(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/code-index/callees", static async (string? @ref, int? depth, string? repo, IService<GetCodeCalleesRequest, CodeCallsView> callees, CancellationToken ct) =>
        {
            var answer = await callees.Handle(new GetCodeCalleesRequest(@ref ?? string.Empty, depth ?? 1, repo), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
