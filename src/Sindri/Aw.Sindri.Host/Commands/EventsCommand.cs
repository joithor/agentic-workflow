using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.GetInstanceEvents;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow events &lt;address&gt; [--follow] [--json]' (engine spec §6.3; README X16; engine P4 unit (e)): one line
/// per event the instance mirrored into the ledger, oldest first ('seq  time  type  from -> to  trigger outcome  run r
/// actor'), or 'no events'. '--json' prints the response; with '--follow' it keeps reading every second, a line (or with
/// '--json' one JSON object) per new event, until interrupted. Exit 0; 2 on an error (a ledger it can't read, a bad address).
/// </summary>
internal static class EventsCommand
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    private const string Usage = "usage: sindri workflow events <address> [--follow] [--json]";

    internal static async Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var follow = args.Contains("--follow", StringComparer.Ordinal);
        if (args.Where(static a => a is not ("--json" or "--follow")).ToArray() is not [var text] || !InstanceAddress.TryFrom(text, out var address))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the one argument is the instance address (e.g. T-1/step/1) ({Usage})")).ConfigureAwait(false);
        }

        var service = services.GetRequiredService<IService<GetInstanceEventsRequest, GetInstanceEventsResponse>>();
        var clock = services.GetRequiredService<TimeProvider>();
        var read = await service.Handle(new GetInstanceEventsRequest(address), ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await output.Error(read.Error).ConfigureAwait(false);
        }

        if (!follow)
        {
            return json ? await output.Json(read.Value, 0).ConfigureAwait(false) : await output.Text(Lines(read.Value.Events, empty: true), 0).ConfigureAwait(false);
        }

        return await Follow(service, address, read.Value, (output, json, clock), ct).ConfigureAwait(false);
    }

    /// <summary>Prints what was read, then reads after the cursor every <see cref="Poll"/> until cancelled.</summary>
    private static async Task<int> Follow(
        IService<GetInstanceEventsRequest, GetInstanceEventsResponse> service, InstanceAddress address, GetInstanceEventsResponse first,
        (Output Output, bool Json, TimeProvider Clock) to, CancellationToken ct)
    {
        var page = first;
        try
        {
            while (true)
            {
                foreach (var e in page.Events)
                {
                    _ = to.Json ? await to.Output.Json(e, 0).ConfigureAwait(false) : await to.Output.Text(Lines([e], empty: false), 0).ConfigureAwait(false);
                }

                await Task.Delay(Poll, to.Clock, ct).ConfigureAwait(false);
                var next = await service.Handle(new GetInstanceEventsRequest(address, page.Cursor), ct).ConfigureAwait(false);
                if (next.IsError)
                {
                    return await to.Output.Error(next.Error).ConfigureAwait(false);
                }

                page = next.Value;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return 0;   // interrupted: --follow ends cleanly
        }
    }

    /// <summary>One line per event; 'no events' for none when <paramref name="empty"/>.</summary>
    internal static string Lines(IReadOnlyList<InstanceEventView> events, bool empty) =>
        events.Count == 0
            ? empty ? InstanceEvents.None + "\n" : string.Empty
            : string.Concat(events.Select(static e => Line(e) + "\n"));

    private static string Line(InstanceEventView e)
    {
        var parts = new List<string>(7)
        {
            e.Sequence.ToString(CultureInfo.InvariantCulture),
            e.OccurredAt.UtcDateTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            e.Type,
        };
        var t = e.Transition;
        if (t.To is { } to)
        {
            parts.Add(t.From is { Length: > 0 } from ? $"{from} -> {to}" : $"-> {to}");
        }

        if (t.Trigger is { } trigger)
        {
            parts.Add(t.Outcome is { } outcome ? $"{trigger} {outcome}" : trigger);
        }

        if (t.Run is { } run)
        {
            parts.Add($"run {run}");
        }

        parts.Add(e.Actor);
        return string.Join("  ", parts);
    }
}
