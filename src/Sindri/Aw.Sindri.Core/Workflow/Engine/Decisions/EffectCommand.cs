using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>An outbox command a decision writes in its Commit (spec W13, §5.4); the dispatcher sends it after 'notBefore'.</summary>
/// <param name="Id">The UUIDv8 from '{trigger key}:{effect index}'.</param>
/// <param name="Effect">The effect block ('effect.notify.desk', 'effect.claim.ref' …).</param>
/// <param name="Identity">Its stable identity.</param>
/// <param name="Origin">Engine or recipe.</param>
/// <param name="With">Its arguments, as the recipe gave them.</param>
public sealed record EffectCommand(
    OutboxCommandId Id,
    BlockId Effect,
    EffectIdentity Identity,
    CommandOrigin Origin,
    IReadOnlyDictionary<string, WithValue> With);
