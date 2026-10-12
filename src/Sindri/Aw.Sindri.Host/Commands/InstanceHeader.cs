using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The status header (engine spec §6.4): '&lt;address&gt;  &lt;recipe&gt;@&lt;hash12&gt;  v&lt;n&gt;  &lt;state&gt;[: &lt;detail&gt;]
/// since &lt;hh:mm&gt; (&lt;age&gt;)'. Engine P4 unit (c) prints it after 'complete'; unit (e)'s status layouts own it, and
/// this one gives way to theirs when the units merge. 'v&lt;n&gt;' is the recipe's version until the instance carries one.
/// </summary>
internal static class InstanceHeader
{
    /// <summary>The header of <paramref name="view"/> at <paramref name="now"/>.</summary>
    /// <param name="view">The instance.</param>
    /// <param name="now">The time the age is measured to.</param>
    /// <returns>The line, without a newline.</returns>
    public static string Line(InstanceView view, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        var hash = view.Recipe.Hash.Value;
        var hash12 = hash[(hash.IndexOf(':', StringComparison.Ordinal) + 1)..][..12];
        var detail = view.Detail ?? (view.Question?.Reason is { } reason ? Reason(reason) : view.Outcome?.Value);
        var state = detail is null ? view.State : $"{view.State}: {detail}";
        var age = now - view.Since;
        var since = age >= TimeSpan.FromDays(1)
            ? view.Since.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
            : view.Since.ToString("HH:mm", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture,
            $"{view.Address.Value}  {view.Recipe.Id.Value}@{hash12}  v{view.Recipe.Version}  {state}  since {since} ({Duration(age)})");
    }

    /// <summary>A duration as §6.4 writes it: '1d 22h 48m', or '40s' under a minute; never negative.</summary>
    private static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)span.TotalSeconds)}s");
        }

        var parts = new List<string>(3);
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        if (span.Minutes > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        }

        return string.Join(' ', parts);
    }

    private static string Reason(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };
}
