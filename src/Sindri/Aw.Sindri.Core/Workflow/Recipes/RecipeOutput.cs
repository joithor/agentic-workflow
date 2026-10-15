using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe output declaration (spec §3.2, UI E-22): '{ key, label?, type?, from }'. A declaration only; it adds no wiring.</summary>
/// <param name="Key">The fact the recipe produces for a human.</param>
/// <param name="Label">The app's label for it.</param>
/// <param name="Type">The fact type (<see cref="FactTypes"/>; text when absent): which viewer the app uses.</param>
/// <param name="From">The state that produces the fact; the app places it under that state.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeOutput(FactKey Key, string? Label, string? Type, string From);
