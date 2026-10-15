using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe input in its long form (spec §3.2): '{ key, label?, type?, kind? }'; the short form is the bare key.</summary>
/// <param name="Key">The fact the start supplies.</param>
/// <param name="Label">The start form's label.</param>
/// <param name="Type">The fact type (<see cref="FactTypes"/>; text when absent): which viewer and start-form field the app uses.</param>
/// <param name="Kind">The subject kind (<see cref="SubjectKinds"/>), for the first input only: what the instance is about; absent = the kind the type implies.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInput(FactKey Key, string? Label, string? Type, string? Kind);
