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

        if (filter.Urgency is { } urgency)
        {
            parts.Add(WithDefault("Urgency", $"Urgency = {Literal(Urgencies.Word(urgency))}", urgency == RecipeUrgency.Work));
        }

        Text(parts, "Item", filter.Item);
        Text(parts, "Subject", filter.Subject);
        if (filter.Root is { } root)
        {
            parts.Add(WithDefault("Root", "Root = " + Bool(root), root));
        }

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    /// <summary>
    /// A filter on an attribute the instance may never have set matches the instances without it when it asks for the
    /// attribute's default: an instance started before the attribute existed reads as 'work' and as a root.
    /// </summary>
    private static string WithDefault(string attribute, string equals, bool isDefault) =>
        isDefault ? $"({equals} OR {attribute} IS NULL)" : equals;

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
