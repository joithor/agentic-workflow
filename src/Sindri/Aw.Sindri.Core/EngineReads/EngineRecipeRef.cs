using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>The recipe an engine instance runs (API spec §3.8, E-30).</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The hash of the version it runs; null when the instance never set it.</param>
public sealed record EngineRecipeRef(RecipeId Id, string? Hash);
