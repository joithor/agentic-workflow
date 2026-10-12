using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// An effect a transition runs (spec §5.7): the interpreter runs it as an activity after the transition's ledger event,
/// a dispatch delay and a recheck. Its identity, not an id of its own, is what a retry or a re-emission keeps.
/// </summary>
/// <param name="Effect">The effect block ('effect.notify.desk', 'effect.claim.ref' …).</param>
/// <param name="Identity">Its stable identity.</param>
/// <param name="Origin">Engine or recipe.</param>
/// <param name="With">Its arguments, as the recipe gave them.</param>
public sealed record EffectCommand(
    BlockId Effect,
    EffectIdentity Identity,
    CommandOrigin Origin,
    IReadOnlyDictionary<string, WithValue> With);
