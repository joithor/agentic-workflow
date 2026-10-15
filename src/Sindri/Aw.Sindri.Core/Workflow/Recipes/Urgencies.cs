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
}
