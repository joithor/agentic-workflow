using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context code &lt;anchor&gt;... [--draft | --map-version V] [--json]' (scoping station C3): a client of GET /sindri/context/code. Exit 0 with an answer; 1 for attention (not running,
/// unknown anchor, no map version, sessions off); 2 for a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class ContextCodeVerbs(ControlPlaneClient client, Output output)
{
    private const string Form = "context code <anchor>... [--draft | --map-version <v>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (Parse(args) is not { } o)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'context code {string.Join(' ', args)}' is not a code command: {Form}")).ConfigureAwait(false);
        }

        if (o.Draft && o.MapVersion is not null)
        {
            return await Failed(ContextErrors.QueryInvalid.With("'--draft' and '--map-version' are two shapes of one question (fix: pass one of them, not both)")).ConfigureAwait(false);
        }

        var query = "?" + string.Join('&', o.Anchors.Select(static a => "anchor=" + Uri.EscapeDataString(a))) + (o.Draft ? "&draft=true" : string.Empty)
            + (o.MapVersion is null ? string.Empty : "&mapVersion=" + Uri.EscapeDataString(o.MapVersion));
        var answer = await client.GetOrQuery<GetContextCodeRequest, CodeView>("/sindri/context/code", query, new GetContextCodeRequest(o.Anchors, o.Draft, o.MapVersion), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextCodeText.Code(answer.Value.Value, fence: client.SessionReason is null), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));

    /// <summary>One or more anchors with [--draft], [--map-version V] and [--json] in any order; null for none, an unknown flag or a repeated or valueless one.</summary>
    private static (string[] Anchors, bool Draft, string? MapVersion, bool Json)? Parse(string[] args)
    {
        var anchors = new List<string>();
        string? version = null;
        bool draft = false, json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--draft" when !draft:
                    draft = true;
                    break;
                case "--json" when !json:
                    json = true;
                    break;
                case "--map-version" when version is null && i + 1 < args.Length:
                    version = args[++i];
                    break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                    return null;
                default:
                    anchors.Add(args[i]);
                    break;
            }
        }

        return anchors.Count == 0 ? null : ([.. anchors], draft, version, json);
    }
}
