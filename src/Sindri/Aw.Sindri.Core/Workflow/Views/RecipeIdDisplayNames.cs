using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// The names until the engine supplies them: a recipe with no known display name is named by its id made readable, 'workflow.dev-triage' as 'Dev triage' (the spec is silent
/// for a recipe with no name, and a raw id is never a name a person should read).
/// </summary>
public sealed class RecipeIdDisplayNames : IRecipeDisplayNames
{
    private const string Prefix = "workflow.";

    /// <summary>The one instance, for a caller built without a lookup.</summary>
    public static RecipeIdDisplayNames Instance { get; } = new();

    /// <summary>The id without 'workflow.', its words apart and the first one capitalised ('workflow.bug-fix' as 'Bug fix').</summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The readable name; never empty.</returns>
    public static string Readable(RecipeId id)
    {
        var bare = id.Value.StartsWith(Prefix, StringComparison.Ordinal) ? id.Value[Prefix.Length..] : id.Value;
        var words = bare.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? "Workflow" : string.Concat(char.ToUpper(words[0][0], CultureInfo.InvariantCulture).ToString(), string.Join(' ', words)[1..]);
    }

    /// <inheritdoc/>
    public string Of(RecipeId id) => Readable(id);
}
