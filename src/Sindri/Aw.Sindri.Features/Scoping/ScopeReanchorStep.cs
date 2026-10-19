using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.reanchor', on the block runtime: <c language="text">ScopingSteps.Reanchor</c>'s code over the changed map. The map's own node keys and the
/// anchors already held always count; the context engine's text shape over the surfaces' titles adds suggestions when it answers, and a map it cannot
/// answer for (no repo, no profile, an engine error) is left out, as the desk leaves out a missing 'contextQuery'.
/// </summary>
/// <param name="shape">The engine's text shape.</param>
internal sealed class ScopeReanchorStep(IScopingTextShape shape) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.reanchor");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var titles = ScopeJson.Items(Fact(context, "scopeMap"), "surfaces").Select(static s => ScopeJson.Str(s, "title")).OfType<string>();
        var query = await shape.Anchors(context.Repo, string.Join('\n', titles), cancellationToken).ConfigureAwait(false);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!query.IsError)
        {
            facts["contextQuery"] = ContextQuery(query.Value);
        }

        return Facts(facts);
    }
}
