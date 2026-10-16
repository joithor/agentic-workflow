using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneReply;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lane-reply' command: a lane's answer to the meta (the lane's stop hook pipes the text after "REPLY TO META:" into it). Silent and successful when no team is set up.</summary>
internal static class LaneReplyCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lane-reply",
        new CommandSpec("lane-reply (--text <text> | --file <path> | --stdin) [--json]   (the lane comes from AW_META_TEAM and AW_META_LANE)", [], ["text", "file"], ["stdin"]),
        NeedsConfig: false,
        Run);

    private static async Task<int> Run(CommandContext context)
    {
        var identity = LaneIdentity.FromEnvironment(context.Io.Env);
        if (identity.IsError)
        {
            return CommandOutput.Fail(context, identity.Error);
        }

        var text = await LaneInput.Read(context, allowStdin: true).ConfigureAwait(false);
        return text.IsError
            ? CommandOutput.Fail(context, text.Error)
            : await CommandOutput.Call<LaneReplyRequest, LaneReplyResponse>(context, new LaneReplyRequest(identity.Value, text.Value), Text).ConfigureAwait(false);
    }

    private static string Text(LaneReplyResponse response) =>
        response.Recorded ? $"recorded {response.Id?.Value}" : string.Empty;
}
