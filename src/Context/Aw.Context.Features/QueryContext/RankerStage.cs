using Aw.Context.Core.Models;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// Stages 5 and 6 of the query (spec §8.2): the feature scores, then the optional rerank of the top items by a model (the only model call on the query path; budgeted, scrubbed, and its answer treated
/// as untrusted: it can only order items the query already found). A rerank that fails leaves the order as it was and is told as degraded 090.
/// </summary>
/// <param name="model">The model port.</param>
/// <param name="clock">The time.</param>
internal sealed class RankerStage(IModelPort model, TimeProvider clock)
{
    /// <summary>Runs the stages the plan asks for.</summary>
    /// <param name="fused">The fused items, best first.</param>
    /// <param name="plan">The stages to run.</param>
    /// <param name="signals">What the map says about each item, by canonical key.</param>
    /// <param name="egress">Scrubs the titles the rerank prompt carries.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ranked items.</returns>
    internal ValueTask<RankedItems> Apply(IReadOnlyList<FusedItem> fused, RankerPlan plan, IReadOnlyDictionary<string, RecordSignals> signals, IEgressScrubber egress, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
