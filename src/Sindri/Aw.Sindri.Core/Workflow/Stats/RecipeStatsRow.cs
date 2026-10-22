namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>A row of the 'recipe-stats' view: the stats as counted and when.</summary>
/// <param name="Stats">The stats.</param>
/// <param name="CachedAt">When they were counted.</param>
public sealed record RecipeStatsRow(RecipeStats Stats, DateTimeOffset CachedAt);
