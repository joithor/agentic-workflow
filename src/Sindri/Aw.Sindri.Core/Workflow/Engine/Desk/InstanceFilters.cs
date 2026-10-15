using System.Text;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>What the Workflows list does with an <see cref="InstanceFilter"/>: the visibility query, the order.</summary>
public static class InstanceFilters
{
    /// <summary>The visibility query fragment for <paramref name="filter"/>: each filter given, ANDed; every value is a quoted literal.</summary>
    /// <param name="filter">The filters.</param>
    /// <returns>E.g. "Recipe = 'workflow.step' AND NeedsYou = true"; null when no filter is given.</returns>
    public static string? ToQuery(InstanceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var parts = new List<string>(7);
        Text(parts, "Recipe", filter.Recipe);
        Text(parts, "State", filter.State);
        if (filter.NeedsYou is { } needsYou)
        {
            parts.Add("NeedsYou = " + Bool(needsYou));
        }

        Text(parts, "Urgency", filter.Urgency);
        Text(parts, "Item", filter.Item);
        Text(parts, "Subject", filter.Subject);
        if (filter.Root is { } root)
        {
            parts.Add("Root = " + Bool(root));
        }

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    /// <summary>The list's order: incidents, then triage, then work; within an urgency the oldest first, then by address.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, ordered.</returns>
    public static IReadOnlyList<InstanceRow> Order(IEnumerable<InstanceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows
            .OrderBy(static r => Rank(r.Urgency))
            .ThenBy(static r => r.StartedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(static r => r.Address, StringComparer.Ordinal)];
    }

    private static int Rank(string urgency)
    {
        var rank = Urgencies.All.ToList().IndexOf(urgency);
        return rank < 0 ? Urgencies.All.Count : rank;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static void Text(List<string> parts, string attribute, string? value)
    {
        if (value is not null)
        {
            parts.Add($"{attribute} = {Literal(value)}");
        }
    }

    /// <summary>A string literal: single-quoted, with a backslash or quote inside escaped, so a value is never query syntax.</summary>
    private static string Literal(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
        {
            _ = c is '\\' or '\'' ? text.Append('\\').Append(c) : text.Append(c);
        }

        return text.Append('\'').ToString();
    }
}
