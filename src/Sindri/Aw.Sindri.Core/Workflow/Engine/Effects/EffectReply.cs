using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The payload of the reply event an effect's activity appends for its block (spec §2, §5.7, §12; engine P6 unit (a)):
/// 'notify.delivered' or 'claim.created', caused by the effect's 'outbox.dispatched' row. The bridge reads its
/// 'outcome'; 'effect' names the effect identity it answers.
/// </summary>
/// <param name="Outcome">The reply's outcome ('delivered', 'created', 'claimExists').</param>
/// <param name="Effect">The effect identity it answers.</param>
/// <param name="Facts">The facts the effect's reply carries, by fact key (the waiting frame receives them as the wait's facts); written only when there are some, so a reply without facts is the payload it always was.</param>
public sealed record EffectReply(
    string Outcome,
    string Effect,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, FactValue>? Facts = null);
