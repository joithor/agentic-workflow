using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The words a person sees for a workflow and its inputs (owner feedback F4, 2026-10-19). The API serves a recipe's display name; an older Sindri
/// serves none, so the id is made readable here instead. A raw id ("workflow.build") is never the name. Shared by every page that names a workflow.
/// </summary>
internal static class WorkflowNames
{
    private const string Prefix = "workflow.";

    /// <summary>The name of a workflow.</summary>
    /// <param name="recipeId">The recipe id, as served.</param>
    /// <param name="displayName">The served display name; null, blank or just the recipe id from an older Sindri.</param>
    /// <returns>The display name, else the id without "workflow." and title-cased ("workflow.bug-fix" reads "Bug Fix").</returns>
    internal static string Of(string recipeId, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(recipeId);
        if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(displayName.Trim(), recipeId, StringComparison.Ordinal))
        {
            return displayName;
        }

        var bare = recipeId.StartsWith(Prefix, StringComparison.Ordinal) ? recipeId[Prefix.Length..] : recipeId;
        var words = bare.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? "Workflow" : string.Join(' ', words.Select(Capitalise));
    }

    /// <summary>The name of an input the recipe gave no label: its key in plain words.</summary>
    /// <param name="key">The key ("fact:scopeInput").</param>
    /// <returns>"Scope input".</returns>
    internal static string InputName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var colon = key.LastIndexOf(':');
        var bare = colon >= 0 ? key[(colon + 1)..] : key;
        var spaced = new StringBuilder();
        foreach (var c in bare)
        {
            _ = c is '_' or '-' or '.'
                ? spaced.Append(' ')
                : char.IsUpper(c) && spaced.Length > 0 && spaced[^1] != ' '
                    ? spaced.Append(' ').Append(char.ToLowerInvariant(c))
                    : spaced.Append(c);
        }

        var text = spaced.ToString().Trim();
        return text.Length == 0 ? key : string.Concat(char.ToUpper(text[0], CultureInfo.InvariantCulture).ToString(), text.AsSpan(1));
    }

    private static string Capitalise(string word) => string.Concat(char.ToUpper(word[0], CultureInfo.InvariantCulture).ToString(), word.AsSpan(1));
}
