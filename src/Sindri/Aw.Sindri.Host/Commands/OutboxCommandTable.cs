using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Features.ListOutbox;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'outbox' verb group (engine spec §6.3; engine P6 unit (c)): 'outbox list [--pending | --dispatched] [--json]',
/// one line per effect emission from the ledger's effect rows (class query), or 'no commands'. '--json' prints the
/// response. Exit 0; 2 on an error.
/// </summary>
internal static class OutboxCommandTable
{
    private const string Usage = "usage: sindri outbox list [--pending | --dispatched] [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["list", .. var rest] when Filter(rest) is { } filter => List(filter.State, filter.Json, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["outbox", .. args])}' is not a sindri command ({Usage})")),
    };

    /// <summary>The state filter and '--json', in either order; null for anything else.</summary>
    private static (EffectState? State, bool Json)? Filter(string[] rest)
    {
        var json = rest.Contains("--json", StringComparer.Ordinal);
        return rest.Where(static a => a != "--json").ToArray() switch
        {
            [] => (null, json),
            ["--pending"] => (EffectState.Pending, json),
            ["--dispatched"] => (EffectState.Dispatched, json),
            _ => null,
        };
    }

    private static async Task<int> List(EffectState? state, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        var read = await services.GetRequiredService<IService<ListOutboxRequest, ListOutboxResponse>>().Handle(new ListOutboxRequest(state), ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await output.Error(read.Error).ConfigureAwait(false);
        }

        return json ? await output.Json(read.Value, 0).ConfigureAwait(false) : await output.Text(Lines(read.Value.Commands), 0).ConfigureAwait(false);
    }

    /// <summary>One line per effect ('seq  time  state  kind  origin  instance  identity  code'), or 'no commands'. Engine P6 unit (c).</summary>
    internal static string Lines(IReadOnlyList<OutboxCommandView> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        throw new NotSupportedException("engine P6 unit (c): the outbox list lines");
    }
}
