using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The answer a status layout offers (E-23).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="ActionId">The catalog action id of this answer kind (<c>ApplyCommand:answer-grant</c>, <c>-grant-tokens</c>, <c>-accept</c> are protected; <c>-text</c>, <c>-resume</c>, <c>-reject</c> are desk).</param>
/// <param name="Class">The catalog row's action class: <c>desk</c> or <c>protected</c>.</param>
/// <param name="Cli">The exact command printed.</param>
/// <param name="AtTerminal">Whether it needs a terminal.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusAnswer(
    AnswerKind Kind,
    ActionId ActionId,
    ActionClass Class,
    string Cli,
    bool AtTerminal);
