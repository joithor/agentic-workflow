using System.Globalization;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.QueueGate;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'queue-gate' command: gates the next pull requests of the merge queue as a train; exit 0 when every one passes, 1 otherwise.</summary>
internal static class QueueGateCommand
{
    internal static CommandEntry Entry { get; } = new(
        "queue-gate",
        new CommandSpec("queue-gate [pr...] [--max K] [--json]", [], ["max"], [], AnyMore: true),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var prs = new List<int>();
        foreach (var text in context.Line.Positionals)
        {
            if (!PrArgument.TryParse(text, out var pr))
            {
                return Task.FromResult(PrArgument.Usage(context, Entry));
            }

            prs.Add(pr);
        }

        int? max = null;
        if (context.Line.Values.ContainsKey("max"))
        {
            if (!int.TryParse(context.Line.Value("max"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                return Task.FromResult(CommandOutput.Usage(context.Io, "--max must be a number from 1", Entry.Spec.Usage));
            }

            max = parsed;
        }

        return CommandOutput.Call<QueueGateRequest, QueueGateResponse>(context, new QueueGateRequest(prs, max), Text, static r => r.Entries.Any(e => e.State != TrainState.Pass));
    }

    private static string Text(QueueGateResponse response) =>
        response.Entries.Count == 0
            ? "queue-gate: no pull requests to gate"
            : string.Join(
                Environment.NewLine,
                [
                    .. response.Gates.SelectMany(g => GateRunCommand.Lines(g, stack: true).Append(GateRunCommand.Result(g))),
                    .. response.Heavy is { } heavy ? [heavy.Line()] : Array.Empty<string>(),
                    .. response.Entries.Where(static e => e.State is TrainState.Dropped or TrainState.Skipped or TrainState.Error && e.Position == 0)
                        .Select(static e => string.Create(CultureInfo.InvariantCulture, $"{e.State.ToString().ToUpperInvariant()} #{e.Pr}: {e.Note}")),
                    .. TrainText.Summary(response.Main, response.Entries),
                ]);
}
