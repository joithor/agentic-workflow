using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.anchor', on the block runtime: the brief's anchors from the context engine's text shape (<see cref="IScopingTextShape"/>) over the brief's text,
/// then <c language="text">ScopingSteps.Anchor</c>'s code. The engine failing to answer fails the Step, which parks for a person (the anchors gather searches from).
/// </summary>
/// <param name="shape">The engine's text shape.</param>
internal sealed class ScopeAnchorStep(IScopingTextShape shape) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.anchor");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = ScopeJson.Str(Fact(context, "brief"), "text") ?? string.Empty;
        var query = await shape.Anchors(context.Repo, text, cancellationToken).ConfigureAwait(false);
        return query.IsError ? query.Error : Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["contextQuery"] = ContextQuery(query.Value) });
    }
}
