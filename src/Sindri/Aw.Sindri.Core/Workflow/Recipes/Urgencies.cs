namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The recipe urgencies of spec §3.2 (UI E-21): the app orders Home's Workflows strip by them; the engine never reads them (RCP-URG-001).</summary>
public static class Urgencies
{
    /// <summary>The urgency of a recipe that declares none.</summary>
    public const string Default = "work";

    /// <summary>The three urgencies, most urgent first.</summary>
    public static IReadOnlyList<string> All { get; } = ["incident", "triage", "work"];

    /// <summary>Whether <paramref name="urgency"/> is one of <see cref="All"/>.</summary>
    public static bool IsKnown(string urgency) => All.Contains(urgency, StringComparer.Ordinal);

    /// <summary>The word of <paramref name="urgency"/> as recipes and the search attribute write it.</summary>
    /// <param name="urgency">The urgency.</param>
    /// <returns>'incident', 'triage' or 'work'.</returns>
    public static string Word(RecipeUrgency urgency) => urgency switch
    {
        RecipeUrgency.Incident => "incident",
        RecipeUrgency.Triage => "triage",
        RecipeUrgency.Work or _ => Default,
    };

    /// <summary>The enum value of a recipe's urgency word; an absent word is <see cref="RecipeUrgency.Work"/>.</summary>
    /// <param name="urgency">The word, or null.</param>
    /// <returns>The urgency.</returns>
    /// <exception cref="ArgumentException">The word is none of <see cref="All"/> (RCP-URG-001 keeps a validated recipe from it).</exception>
    public static RecipeUrgency Parse(string? urgency) => urgency switch
    {
        null or Default => RecipeUrgency.Work,
        "incident" => RecipeUrgency.Incident,
        "triage" => RecipeUrgency.Triage,
        _ => throw new ArgumentException($"'{urgency}' is not a recipe urgency (RCP-URG-001)", nameof(urgency)),
    };
}
