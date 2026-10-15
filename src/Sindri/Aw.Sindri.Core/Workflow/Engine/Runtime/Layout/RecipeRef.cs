using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The recipe an instance runs, as the list shows it (E-20).</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The full approved hash.</param>
/// <param name="Version">The recipe's version.</param>
/// <param name="Urgency">How soon the recipe's instances need a human: 'incident', 'triage' or 'work'.</param>
public sealed record RecipeRef(
    RecipeId Id,
    string Hash,
    int Version,
    Recipes.RecipeUrgency Urgency);
