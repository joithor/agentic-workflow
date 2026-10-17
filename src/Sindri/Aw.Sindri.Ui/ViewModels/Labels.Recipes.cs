using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the recipe sheet (screens spec §4.3). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The sheet's note when a re-read no longer finds the recipe it shows.</summary>
    internal const string RecipeGone = "This recipe is no longer served; what is shown was its last state.";

    /// <summary>A recipe hash as the CLI shows it: its first twelve hex digits, without the scheme (the API serves the whole hash on a recipe).</summary>
    /// <param name="hash">The hash as served, sha256: and 64 hex digits.</param>
    /// <returns>Twelve hex digits.</returns>
    internal static string Hash12(string hash)
    {
        var digits = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash["sha256:".Length..] : hash;
        return digits.Length > 12 ? digits[..12] : digits;
    }

    /// <summary>A recipe's urgency in the word the API serves it in.</summary>
    /// <param name="urgency">The urgency.</param>
    /// <returns>The word.</returns>
    internal static string UrgencyWord(RecipeUrgency urgency) => urgency switch
    {
        RecipeUrgency.Incident => "incident",
        RecipeUrgency.Triage => "triage",
        RecipeUrgency.Work => "work",
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, message: null),
    };
}
