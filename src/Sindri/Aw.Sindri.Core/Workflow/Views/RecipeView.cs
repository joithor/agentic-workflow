using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The recipe view: the recipe's stamp (id, hash, version; the same one an instance carries in `recipe`), served now ahead of the engine's full body, and the E-22 additions (inputs, outputs, urgency, path-ordered states and loops).</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="DisplayName">The name shown instead of the id (setup-once spec §7.7); null, and left out of the JSON, when the engine knows none yet (the field is required on a recipe file once RCP-DISPLAY-001 lands). The app makes the id readable itself.</param>
/// <param name="Hash">The recipe hash, sha256: and 64 hex digits.</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Name">The recipe's name template as written, or null (spec section 3.11).</param>
/// <param name="Inputs">The inputs.</param>
/// <param name="Outputs">The outputs.</param>
/// <param name="Urgency">The urgency.</param>
/// <param name="OrderedBy">How the states were ordered: 'path', or 'transition-list' with more than one initial path.</param>
/// <param name="States">States in path order.</param>
/// <param name="Loops">The loops.</param>
/// <param name="TransitionLabels">The transitions that carry a plain-words label, in file order; null (left out of the JSON) when none does.</param>
/// <param name="Why">The recipe's plain-words reason, or null (left out of the JSON).</param>
/// <param name="Transitions">Every transition, in file order; null (left out of the JSON) when the view was built without them.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeView(
    RecipeId Id,
    RecipeHash Hash,
    int Version,
    string? Name,
    IReadOnlyList<RecipeInputView> Inputs,
    IReadOnlyList<RecipeOutputView> Outputs,
    RecipeUrgency Urgency,
    PathOrdering OrderedBy,
    IReadOnlyList<RecipeStateView> States,
    IReadOnlyList<RecipeLoopView> Loops,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeTransitionLabelView>? TransitionLabels = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeTransitionView>? Transitions = null);
