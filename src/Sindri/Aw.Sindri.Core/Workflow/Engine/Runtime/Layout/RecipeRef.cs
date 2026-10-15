namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The recipe an instance runs, as the list shows it (E-20).</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The full approved hash.</param>
/// <param name="Version">The recipe's version.</param>
/// <param name="Urgency">How soon the recipe's instances need a human.</param>
public sealed record RecipeRef(
    string Id,
    string Hash,
    int Version,
    Urgency Urgency);
