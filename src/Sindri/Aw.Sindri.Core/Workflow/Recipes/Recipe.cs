using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A recipe in its normalized long form (spec §3.2, W3): what the reader binds after turning short forms into long
/// ones, what the JSON Schema describes and what the hash covers (without <see cref="Hash"/>). Collections are never
/// null in the long form: an absent list or map is empty.
/// </summary>
/// <param name="Schema">The schema version; 1.</param>
/// <param name="Id">The recipe's id, 'workflow.&lt;name&gt;'.</param>
/// <param name="Version">The author's version number (RCP-VERSION-001/002).</param>
/// <param name="Description">What the recipe is for.</param>
/// <param name="Base">The recipe a generated recipe was made from (written by Sindri's generator; RCP-CAP-004 on a hand-written one).</param>
/// <param name="Urgency">'incident', 'triage' or 'work' (spec §3.2, UI E-21); the normalizer writes 'work' when absent, so both hash the same.</param>
/// <param name="Name">A template (spec §3.11) for the instance's name, rendered once at start; null when the recipe doesn't name its instances.</param>
/// <param name="Inputs">The facts a start supplies, with how the app's start form asks for them (the first is the subject).</param>
/// <param name="Outputs">What the recipe produces for a human, each placed under its 'from' state (spec §3.2, UI E-22); declarations only.</param>
/// <param name="Initial">The state an instance starts in: an active or wait state.</param>
/// <param name="States">The states by name, in file order.</param>
/// <param name="Transitions">The transitions, in file order (the first whose 'when' holds is the matched one).</param>
/// <param name="Loops">The loops by name.</param>
/// <param name="Budget">The recipe-wide budget.</param>
/// <param name="Evals">The eval files, relative to the recipe's directory (spec §9.2).</param>
/// <param name="Hash">The hash the file states, checked when present (RCP-HASH-001).</param>
/// <param name="DisplayName">The name the app shows instead of the id: sentence case, at most 60 characters, what the recipe does (RCP-DISPLAY-001 requires it of a new recipe; the parameter has a default, so a recipe stored before the field existed still reads, with none).</param>
/// <param name="Why">Optional plain words (at most 200 characters, no ids or code names; RCP-DISPLAY-002) on why the recipe exists; written into the hashed form only when set.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Recipe(
    int Schema,
    RecipeId Id,
    int Version,
    string? Description,
    RecipeRef? Base,
    string Urgency,
    string? Name,
    IReadOnlyList<RecipeInput> Inputs,
    IReadOnlyList<RecipeOutput> Outputs,
    string Initial,
    IReadOnlyDictionary<string, RecipeState> States,
    IReadOnlyList<RecipeTransition> Transitions,
    IReadOnlyDictionary<string, LoopSpec> Loops,
    BudgetSpec? Budget,
    IReadOnlyList<string> Evals,
    RecipeHash? Hash,
    string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null);
