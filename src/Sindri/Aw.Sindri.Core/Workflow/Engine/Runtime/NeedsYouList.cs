namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What needs you, as 'workflow list --needs-me' shows it (engine spec §6.3, README X5): <b>Decisions</b> (parks,
/// escalations, takeovers) and then <b>Tasks</b> (open desk tasks), each ordered by address. A parent whose open
/// question mirrors a child that is also listed appears once, under the child.
/// </summary>
/// <param name="Decisions">Parked, escalated and taken-over instances.</param>
/// <param name="Tasks">Instances holding an open desk task.</param>
public sealed record NeedsYouList(IReadOnlyList<InstanceView> Decisions, IReadOnlyList<InstanceView> Tasks)
{
    /// <summary>The empty state's words.</summary>
    public const string Nothing = "nothing needs you";

    /// <summary>Whether nothing needs you.</summary>
    public bool IsEmpty => Decisions.Count == 0 && Tasks.Count == 0;

    /// <summary>Sorts <paramref name="views"/> into the two sections; a view that needs nobody is left out.</summary>
    /// <param name="views">The instances the NeedsYou search attribute found.</param>
    /// <returns>The list.</returns>
    public static NeedsYouList Of(IEnumerable<InstanceView> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var all = views.OrderBy(static v => v.Address.Value, StringComparer.Ordinal).ToList();
        var listed = all.Select(static v => v.Address.Value).ToHashSet(StringComparer.Ordinal);
        var shown = all.Where(v => !(v.Question is not null && v.Children.Any(c => listed.Contains(c.Address.Value)))).ToList();
        return new NeedsYouList(
            [.. shown.Where(static v => v.Outcome is null && v.Question is not null)],
            [.. shown.Where(static v => v.Outcome is null && v.Question is null && v.Task is not null)]);
    }
}
