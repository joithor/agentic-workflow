using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The additions the recipe view makes (E-22): inputs, outputs, urgency, path-ordered states and loops.</summary>
/// <param name="Inputs">The inputs.</param>
/// <param name="Outputs">The outputs.</param>
/// <param name="Urgency">The urgency.</param>
/// <param name="OrderedBy">How the states were ordered: 'path', or 'transition-list' with more than one initial path.</param>
/// <param name="States">States in path order.</param>
/// <param name="Loops">The loops.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeView(
    IReadOnlyList<RecipeInputView> Inputs,
    IReadOnlyList<RecipeOutputView> Outputs,
    RecipeUrgency Urgency,
    PathOrdering OrderedBy,
    IReadOnlyList<RecipeStateView> States,
    IReadOnlyList<RecipeLoopView> Loops);
