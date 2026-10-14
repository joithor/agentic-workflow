using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A 'retry' re-emits the effects of the transition that entered the state (engine spec §3.4, §5.5, §5.7; engine P6 unit
/// (a)): each keeps the stable identity '{instance}:{entry}:{index}' it had, so the effect block's existence check makes
/// the repeat harmless.
/// </summary>
public sealed class EffectReemissionTests
{
    [Fact, Trait("Lane", "unit")]
    public void Retry_reemits_the_entering_transitions_effects_with_their_identity()
    {
        var claimed = At(Item, "claimed", entry: 3);   // startable -> claimed emits effect.claim.ref; its timeout retries

        var effect = Assert.Single(EffectRules.Reemitted(claimed, Item));

        Assert.Equal(BlockId.From("effect.claim.ref"), effect.Effect);
        Assert.Equal(new EffectIdentity(Id, Entry: 3, Index: 0), effect.Identity);
        Assert.Equal($"{Id.Value}:3:0", effect.Identity.Text);
        Assert.Equal(CommandOrigin.Recipe, effect.Origin);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_no_transition_with_effects_enters_reemits_nothing()
    {
        Assert.Empty(EffectRules.Reemitted(At(Item, "triaged"), Item));
        Assert.Empty(EffectRules.Reemitted(At(Step, "checks", entry: 2), Step));   // checks retries, but produce -> checks has no effects
    }
}
