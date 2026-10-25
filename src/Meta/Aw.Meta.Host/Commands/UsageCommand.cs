using System.Globalization;
using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Usage;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'usage' command: each lane's and the meta's standing against the Claude usage limits. Exit 1 when any is limited.</summary>
internal static class UsageCommand
{
    internal static CommandEntry Entry { get; } = new(
        "usage",
        CommandSpec.None("usage [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<UsageRequest, UsageResponse>(context, new UsageRequest(), Text, static r => r.Attention));

    private static string Text(UsageResponse response) => string.Join(Environment.NewLine, response.Subjects.Select(Row));

    private static string Row(UsageRow row)
    {
        if (row.State == UsageState.Limited)
        {
            var resets = row.ResetsAt is { } at ? at.UtcDateTime.ToString("u", CultureInfo.InvariantCulture) : "unknown";
            return $"{row.Subject,-12} limited  {row.Kind.ToString()?.ToLowerInvariant()} limit, resets {resets}  (seen in {row.Source})";
        }

        return row.State == UsageState.Unknown ? $"{row.Subject,-12} unknown  {row.Reason}" : $"{row.Subject,-12} ok";
    }
}
