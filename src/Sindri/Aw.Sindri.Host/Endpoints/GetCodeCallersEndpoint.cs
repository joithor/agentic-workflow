using Aw.Kernel.Services;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/code-index/callers?ref=&amp;depth=&amp;repo=&amp;implementations=: the edges into a symbol, each with its basis, confidence and coverage.</summary>
internal static class GetCodeCallersEndpoint
{
    internal static RouteGroupBuilder MapGetCodeCallers(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/code-index/callers", static async (string? @ref, int? depth, string? repo, bool? implementations, IService<GetCodeCallersRequest, CodeCallsView> callers, CancellationToken ct) =>
        {
            var answer = await callers.Handle(new GetCodeCallersRequest(@ref ?? string.Empty, depth ?? 1, repo, implementations ?? false), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
