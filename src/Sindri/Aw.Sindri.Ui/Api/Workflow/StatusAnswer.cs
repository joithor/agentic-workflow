using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How the instance is answered.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="ActionId">ActionId, as served.</param>
/// <param name="Class">Class, as served.</param>
/// <param name="Cli">Cli, as served.</param>
/// <param name="AtTerminal">AtTerminal, as served.</param>
/// <param name="Via">"child" on a pointer to a child that decides the answer (the cli is then not an answer form), else null.</param>
/// <param name="Child">The child's address on a "child" pointer, else null.</param>
internal sealed record StatusAnswer(
    AnswerKind Kind,
    string ActionId,
    string Class,
    string Cli,
    bool AtTerminal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Child = null);
