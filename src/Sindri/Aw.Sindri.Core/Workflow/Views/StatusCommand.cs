using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One verb of the instance (E-23).</summary>
/// <param name="Verb">The verb ("answer", "complete", "handback", "takeover", "cancel").</param>
/// <param name="ActionId">The catalog action id of the row.</param>
/// <param name="Class">The catalog row's action class ("pause" for takeover; "desk" or "protected" otherwise).</param>
/// <param name="Cli">The command.</param>
/// <param name="Confirm">The one-sentence confirmation, set on "cancel" only (<see cref="ConfirmText.Cancel"/>); null on every other verb.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusCommand(
    CommandVerb Verb,
    ActionId ActionId,
    ActionClass Class,
    string Cli,
    string? Confirm);
