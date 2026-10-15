using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context show &lt;key&gt; | neighbors &lt;key&gt; [--depth N] | path &lt;a&gt; &lt;b&gt;', each with [--json] (context spec
/// §8.5): clients of GET /sindri/context/node, /neighbors and /path. A key may also be an alias or a node's name. Exit 0
/// with an answer (no path is an answer); 1 for attention (not running, unknown or ambiguous anchor, no map, a depth out
/// of range, session reads off); 2 for a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class ContextGraphVerbs(ControlPlaneClient client, Output output)
{
    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["show", var key, .. var rest] when Json(rest) is { } json => Show(key, json, ct),
        ["neighbors", var key, .. var rest] when Depth(rest) is { } o => Neighbors(key, o, ct),
        ["path", var from, var to, .. var rest] when Json(rest) is { } json => Path(from, to, json, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With(
            $"'context {string.Join(' ', args)}' is not a graph command: context show <key> [--json] | context neighbors <key> [--depth N] [--json] | context path <a> <b> [--json]")),
    };

    private async Task<int> Show(string key, bool json, CancellationToken ct)
    {
        var answer = await client.Get<NodeView>("/sindri/context/node?key=" + Uri.EscapeDataString(key), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextGraphText.Node(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Neighbors(string key, (string? Depth, bool Json) options, CancellationToken ct)
    {
        var depth = options.Depth is null ? string.Empty : "&depth=" + Uri.EscapeDataString(options.Depth);
        var answer = await client.Get<NeighborsView>("/sindri/context/neighbors?key=" + Uri.EscapeDataString(key) + depth, ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : options.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextGraphText.Neighbors(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Path(string from, string to, bool json, CancellationToken ct)
    {
        var answer = await client.Get<PathView>($"/sindri/context/path?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextGraphText.Path(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error)
    {
        var code = await output.Error(error).ConfigureAwait(false);
        return error.Code == "SND-API-001" || ContextErrors.Attention.Contains(error.Code) ? 1 : code;
    }

    private static bool? Json(string[] rest) => rest switch
    {
        [] => false,
        ["--json"] => true,
        _ => null,
    };

    /// <summary>'[--depth N] [--json]' in either order; the depth is checked by the server.</summary>
    private static (string? Depth, bool Json)? Depth(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--json"] => (null, true),
        ["--depth", var n] => (n, false),
        ["--depth", var n, "--json"] => (n, true),
        ["--json", "--depth", var n] => (n, true),
        _ => null,
    };
}
