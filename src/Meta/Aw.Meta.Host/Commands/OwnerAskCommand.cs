using System.Globalization;
using Aw.Meta.Features.OwnerAsk;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'owner-ask' command.</summary>
internal static class OwnerAskCommand
{
    internal static CommandEntry Entry { get; } = new(
        "owner-ask",
        new CommandSpec("owner-ask --text <question> [--json]", ["text"], [], []),
        NeedsConfig: true,
        static context => CommandOutput.Call<AskOwnerRequest, AskOwnerResponse>(
            context,
            new AskOwnerRequest(context.Line.Value("text")),
            static r => string.Create(CultureInfo.InvariantCulture, $"queued as q{r.Number}")));
}
