namespace Aw.Sindri.Features.Plan;

/// <summary>graph.json, derived by code from tasks.json (line spec §6.2, §8.3): each phase's stack, wave and base rule, the waves at the profile's parallel, the critical path and the PR count.</summary>
public static class PlanGraph
{
    /// <summary>Derives graph.json.</summary>
    /// <param name="tasksJson">The text of tasks.json.</param>
    /// <param name="options">The profile values.</param>
    /// <returns>The graph.json text (indented, ending with a newline); null when tasks.json is no task plan.</returns>
    public static string? Derive(string tasksJson, PlanGraphOptions options) => throw new NotSupportedException();
}
