using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>Names clusters with a model through <see cref="IModelPort"/>, within a token cap (SND-CONTEXT-042 when the pass would go over it). It sends file paths only, scrubbed.</summary>
/// <param name="model">The model port.</param>
/// <param name="scrubbers">Builds the scrubbers the prompt passes through.</param>
/// <param name="maxTokens">The most tokens (sent and received) one pass may spend ('context.labeling.maxTokens').</param>
internal sealed class ModelLabeler(IModelPort model, IScrubberFactory scrubbers, int maxTokens) : ILabeler
{
    public ValueTask<Result<IReadOnlyList<ClusterLabel>>> Label(IReadOnlyList<FileCluster> clusters, CancellationToken cancellationToken) =>
        throw Pending.Because(model, scrubbers, maxTokens);
}
