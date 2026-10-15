using System.Globalization;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.StopCheck;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// The 'stop-check' command, the Stop hook's one question: exit 1 means "block this stop" (the open obligations are printed with
/// the exact next command); exit 0 means the session may stop (silent, or one warning line when the loop guard let a stop through);
/// any other exit code is a tool error, which the hook treats as fail-open.
/// </summary>
internal static class StopCheckCommand
{
    internal const int DefaultMaxBlocks = 3;

    internal static CommandEntry Entry { get; } = new(
        "stop-check",
        new CommandSpec("stop-check --session <uuid> --transcript <path> [--hook-active] [--json]", ["session", "transcript"], [], ["hook-active"]),
        NeedsConfig: false,   // a session that is not the meta must be able to stop even when no team is configured
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (!SessionId.TryParse(context.Line.Value("session"), CultureInfo.InvariantCulture, out var session))
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--session must not be blank", Entry.Spec.Usage));
        }

        var max = int.TryParse(context.Io.Env("AW_META_STOP_MAX"), NumberStyles.None, CultureInfo.InvariantCulture, out var configured) && configured > 0
            ? configured
            : DefaultMaxBlocks;
        var request = new StopCheckRequest(session, context.Line.Value("transcript"), context.Line.Has("hook-active"), max);
        return CommandOutput.Call<StopCheckRequest, StopCheckResponse>(context, request, Text, static response => response.Blocked);
    }

    private static string Text(StopCheckResponse response)
    {
        if (response.Warning is { } warning)
        {
            return warning;
        }

        return response.Blocked
            ? string.Join(
                Environment.NewLine,
                [
                    string.Create(CultureInfo.InvariantCulture, $"aw-meta: {response.Open.Count} open obligation(s); close them before stopping."),
                    .. response.Open.SelectMany(static (o, i) => new[]
                    {
                        string.Create(CultureInfo.InvariantCulture, $"{i + 1}. [{o.Kind}] {o.Summary}"),
                        "   next: " + o.NextCommand,
                    }),
                ])
            : string.Empty;
    }
}
