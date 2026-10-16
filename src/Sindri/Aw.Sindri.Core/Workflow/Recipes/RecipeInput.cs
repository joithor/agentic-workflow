using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe input in its long form (spec §3.2): '{ key, label?, type?, kind, generate? }'; the short form is the bare key.</summary>
/// <param name="Key">The fact the start supplies.</param>
/// <param name="Label">The start form's label.</param>
/// <param name="Type">The fact type (<see cref="FactTypes"/>; text when absent): which viewer and start-form field the app uses.</param>
/// <param name="Kind">The subject kind (<see cref="SubjectKinds"/>): what the input is about. Required (RCP-INPUT-001); null only in a recipe that fails that rule.</param>
/// <param name="Generate">A template (spec §3.11) that supplies the value when the start doesn't; text inputs only, over the start time only.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInput(FactKey Key, string? Label, string? Type, string? Kind, string? Generate);
