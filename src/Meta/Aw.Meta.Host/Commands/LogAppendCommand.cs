using Aw.Meta.Features.LogAppend;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'log-append' command.</summary>
internal static class LogAppendCommand
{
    internal static CommandEntry Entry { get; } = new(
        "log-append",
        new CommandSpec("log-append --kind merge|decision|lanes|incident|note --text <text> [--json]", ["kind", "text"], [], []),
        NeedsConfig: true,
        static context => CommandOutput.Call<AppendLogRequest, AppendLogResponse>(
            context,
            new AppendLogRequest(context.Line.Value("kind"), context.Line.Value("text")),
            static r => r.Line));
}
