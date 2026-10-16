namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What needs you, as 'workflow list --needs-me' shows it (engine spec section 6.3, README X5): the rows the NeedsYou search attribute found, a
/// parent whose open question mirrors a child that is also listed appearing once, under the child. The list route applies <see cref="Shown"/>
/// and orders the rows as every list is ordered; the CLI groups them into Decisions and Tasks from the served chips.
/// </summary>
public static class NeedsYouList
{
    /// <summary>The empty state's words.</summary>
    public const string Nothing = "nothing needs you";

    /// <summary>
    /// The views that stay when a parent whose open question mirrors a child that is also listed is dropped (engine spec section 6.3: once, under the
    /// child). The order of <paramref name="views"/> is kept.
    /// </summary>
    /// <param name="views">The instances the NeedsYou search attribute found.</param>
    /// <returns>The views, each question once.</returns>
    public static IEnumerable<InstanceView> Shown(IEnumerable<InstanceView> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var all = views.ToList();
        var listed = all.Select(static v => v.Address.Value).ToHashSet(StringComparer.Ordinal);
        return all.Where(v => !(v.Question is not null && v.Children.Any(c => listed.Contains(c.Address.Value))));
    }
}
