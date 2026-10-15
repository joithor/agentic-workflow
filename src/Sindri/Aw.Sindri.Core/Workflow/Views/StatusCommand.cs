using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One verb of the instance (E-23).</summary>
/// <param name="Verb">The verb.</param>
/// <param name="Action">The action id.</param>
/// <param name="Class">Desk or protected.</param>
/// <param name="Cli">The command.</param>
/// <param name="Confirm">A one-sentence confirmation on a destructive verb; null until the wording is approved.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusCommand(
    string Verb,
    [property: JsonPropertyName("actionId")] string Action,
    string Class,
    string Cli,
    string? Confirm);
