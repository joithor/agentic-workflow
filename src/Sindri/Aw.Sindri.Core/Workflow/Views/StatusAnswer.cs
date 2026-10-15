using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The answer a status layout offers (E-23).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Action">The action id.</param>
/// <param name="Class">Desk or protected.</param>
/// <param name="Cli">The exact command printed.</param>
/// <param name="AtTerminal">Whether it needs a terminal.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusAnswer(
    AnswerKind Kind,
    [property: JsonPropertyName("actionId")] string Action,
    string Class,
    string Cli,
    bool AtTerminal);
