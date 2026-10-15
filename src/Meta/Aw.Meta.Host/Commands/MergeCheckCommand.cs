using System.Globalization;
using Aw.Meta.Core.Merge;
using Aw.Meta.Features.CheckMerge;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'merge-check' command: exit 0 when merge-ready, 1 when any check fails.</summary>
internal static class MergeCheckCommand
{
    internal static CommandEntry Entry { get; } = new(
        "merge-check",
        new CommandSpec("merge-check <pr> [--json]", [], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        PrArgument.TryParse(context.Line.Positional(0), out var pr)
            ? CommandOutput.Call<CheckMergeRequest, CheckMergeResponse>(context, new CheckMergeRequest(pr), Text, static r => !r.Ready)
            : Task.FromResult(PrArgument.Usage(context, Entry));

    internal static IEnumerable<string> Lines(IEnumerable<MergeCheck> checks) =>
        checks.Select(c => $"{(c.Ok ? "PASS" : "FAIL")} {c.Name}: {c.Detail}");

    private static string Text(CheckMergeResponse response) =>
        string.Join(Environment.NewLine, [.. Lines(response.Checks), string.Create(CultureInfo.InvariantCulture, $"{(response.Ready ? "READY" : "NOT READY")} #{response.Pr}")]);
}
