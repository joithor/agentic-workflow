using System.Globalization;
using Aw.Meta.Core.Owner;
using Aw.Meta.Features.Owner;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'owner' command.</summary>
internal static class OwnerCommand
{
    internal static CommandEntry Entry { get; } = new(
        "owner",
        new CommandSpec("owner [--all] [--json]", [], [], ["all"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<ListOwnerRequest, ListOwnerResponse>(context, new ListOwnerRequest(context.Line.Has("all")), Text));

    private static string Text(ListOwnerResponse response) =>
        response.Questions.Count == 0
            ? "no questions for the owner"
            : string.Join(Environment.NewLine, response.Questions.Select(Line));

    private static string Line(OwnerQuestion q) =>
        string.Create(CultureInfo.InvariantCulture, $"q{q.Number}  {q.AskedBy}  {q.Text}") + (q.Answer is { } answer ? $"  => {answer}" : string.Empty);
}
