using Aw.Context.Core.Engine;
using Aw.Context.Core.Subgraph;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context subgraph &lt;key&gt;... [--keys-from &lt;file|-&gt;] [--map-version V] [--depth 0-4] [--json]' (scoping spec C14): a client of GET /sindri/context/subgraph, or of QUERY
/// when the keys don't fit a URL. Exit 0 with an answer (keys the map doesn't hold are listed, not an error); 1 for attention; 2 for a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="stdin">Read for '--keys-from -'.</param>
internal sealed class ContextSubgraphVerbs(ControlPlaneClient client, Output output, TextReader stdin)
{
    private const string Route = "/sindri/context/subgraph";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = ContextSubgraphArguments.Parse(args, stdin);
        if (parsed.IsError)
        {
            return ContextExit.For(parsed.Error, await output.Error(parsed.Error).ConfigureAwait(false));
        }

        var o = parsed.Value;
        var answer = o.UseBody
            ? await client.Query<GetContextSubgraphRequest, SubgraphView>(Route, new GetContextSubgraphRequest(o.Keys, QueryRules.ParseDepth(o.Depth).Value, o.MapVersion), ct).ConfigureAwait(false)
            : await client.Get<SubgraphView>(Route + o.QueryString, ct).ConfigureAwait(false);
        return answer.IsError ? ContextExit.For(answer.Error, await output.Error(answer.Error).ConfigureAwait(false))
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextLensText.Subgraph(answer.Value.Value, client.SessionReason is null), 0).ConfigureAwait(false);
    }
}
