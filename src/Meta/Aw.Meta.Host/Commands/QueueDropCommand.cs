using Aw.Meta.Features.QueueDrop;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'queue-drop' command: removes a pull request's merge-queue entry and logs the reason.</summary>
internal static class QueueDropCommand
{
    internal static CommandEntry Entry { get; } = new(
        "queue-drop",
        new CommandSpec("queue-drop <pr> --reason <text> [--json]", ["reason"], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        PrArgument.TryParse(context.Line.Positional(0), out var pr)
            ? CommandOutput.Call<QueueDropRequest, QueueDropResponse>(context, new QueueDropRequest(pr, context.Line.Value("reason")), static r => r.Line)
            : Task.FromResult(PrArgument.Usage(context, Entry));
}
