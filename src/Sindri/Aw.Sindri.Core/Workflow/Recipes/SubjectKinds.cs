namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The subject kinds of spec §3.3 and the fact type each pairs with (RCP-TYPE-002, RCP-TYPE-003).</summary>
public static class SubjectKinds
{
    /// <summary>The six subject kinds.</summary>
    public static IReadOnlyList<string> All { get; } = ["item", "linear-project", "webhook", "file", "text", "link"];

    /// <summary>Whether <paramref name="kind"/> is one of <see cref="All"/>.</summary>
    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);

    /// <summary>The fact type a kind pairs with ('item' with item, 'file' with file, 'text' with text, 'link' with link, the other two with payload), or null for an unknown kind.</summary>
    public static string? PairedType(string kind) => kind switch
    {
        "item" => "item",
        "file" => "file",
        "text" => "text",
        "link" => "link",
        "linear-project" or "webhook" => "payload",
        _ => null,
    };

    /// <summary>The fact type an input's value has: the declared one, else the type its declared kind pairs with, else the declared default 'text' (spec §3.3).</summary>
    /// <param name="input">A recipe input.</param>
    /// <returns>The type name.</returns>
    public static string TypeOf(RecipeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Type ?? (input.Kind is { } kind ? PairedType(kind) : null) ?? FactTypes.Default;
    }

    /// <summary>What a recipe view serves as the kind of an input a pre-P4c recipe declared without one (the CLI prints it as kind=-).</summary>
    public const string NoKind = "-";

    /// <summary>The fix a pre-P4c recipe's refusals and marks name (RCP-VERSION-001 lets the new file register over the old row).</summary>
    public const string LegacyFix = "register the current file at a new version and approve it";

    /// <summary>
    /// The finding a recipe registered before input kinds has (RCP-INPUT-002: no input; RCP-INPUT-001: an input with no kind), or null for a recipe that has
    /// both. Such a row stays as registered (its hash is not rewritten), so the start, the list and the view check this before they read a kind.
    /// </summary>
    /// <param name="recipe">A registered recipe.</param>
    /// <returns>The rule id with what is missing, or null.</returns>
    public static string? Legacy(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Inputs.Count == 0
            ? "RCP-INPUT-002: the recipe declares no input"
            : recipe.Inputs.FirstOrDefault(static i => i.Kind is null) is { } bare
                ? $"RCP-INPUT-001: input {bare.Key.Value} declares no kind"
                : null;
    }

    /// <summary>The kind an input has: the declared one. RCP-INPUT-001 makes it required, so a recipe that passed <see cref="Legacy"/> always has it.</summary>
    /// <param name="input">A recipe input.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="InvalidOperationException">The recipe declares no kind: the caller skipped <see cref="Legacy"/>.</exception>
    public static string Effective(RecipeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Kind ?? throw new InvalidOperationException($"input {input.Key.Value} declares no kind (RCP-INPUT-001)");
    }
}
