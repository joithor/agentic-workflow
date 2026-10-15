using System.Globalization;
using Aw.Meta.Features.OwnerAnswer;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'owner-answer' command.</summary>
internal static class OwnerAnswerCommand
{
    internal static CommandEntry Entry { get; } = new(
        "owner-answer",
        new CommandSpec("owner-answer <q> --answer <text> [--json]", ["answer"], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var text = context.Line.Positional(0).TrimStart('q', 'Q');
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? CommandOutput.Call<AnswerOwnerRequest, AnswerOwnerResponse>(context, new AnswerOwnerRequest(number, context.Line.Value("answer")), Text)
            : Task.FromResult(CommandOutput.Usage(context.Io, "the question must be a number such as 3 or q3", Entry.Spec.Usage));
    }

    private static string Text(AnswerOwnerResponse response) =>
        $"answered; logged: {response.LogLine}{Environment.NewLine}relay to {response.Question.AskedBy}: {response.Relay}";
}
