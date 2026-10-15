using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The status header (engine spec §6.4): '&lt;address&gt;  &lt;recipe&gt;@&lt;hash12&gt;  v&lt;n&gt;  &lt;state&gt;[: &lt;detail&gt;]
/// since &lt;hh:mm&gt; (&lt;age&gt;)'. 'v&lt;n&gt;' is the instance's version, the count of events it has mirrored into the
/// ledger; times are UTC, with the date once the age is a day or more. Every write verb prints it after its command,
/// 'status' first, and 'list' prints one per line.
/// </summary>
public static class InstanceHeader
{
    /// <summary>The header of <paramref name="view"/> at <paramref name="now"/>.</summary>
    /// <param name="view">The instance.</param>
    /// <param name="now">The time the age is measured to.</param>
    /// <returns>The line, without a newline.</returns>
    public static string Line(InstanceView view, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        var state = view.Detail is { Length: > 0 } detail ? $"{view.State}: {detail}" : view.State;
        return string.Create(CultureInfo.InvariantCulture,
            $"{view.Address.Value}  {view.Recipe.Id.Value}@{StatusText.Hash12(view.Recipe.Hash.Value)}  v{view.Version}  {state}  since {StatusText.Clock(view.Since, now)} ({StatusText.Duration(now - view.Since)})");
    }
}
