using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A transition's plain-words label (setup-once spec §5.3), as the app draws it on the arrow.</summary>
/// <param name="From">The states the transition leaves.</param>
/// <param name="To">The state it enters.</param>
/// <param name="On">The trigger.</param>
/// <param name="Label">The label, for example "gaps found: draft again".</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeTransitionLabelView(IReadOnlyList<string> From, string To, string On, string Label);
