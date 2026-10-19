using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.anchor', on the block runtime: the brief's anchors from the context engine's text shape (<see cref="ScopingTextShape"/>) over the brief's text,
/// then <see cref="ScopingSteps.Anchor"/>'s code. The engine failing to answer fails the Step, which parks for a person (the anchors gather searches from).
/// </summary>
/// <param name="shape">The engine's text shape.</param>
internal sealed class ScopeAnchorStep(IScopingTextShape shape) : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Anchor;

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = ScopeJson.Str(Fact(context, "brief"), "text") ?? string.Empty;
        var query = await shape.Anchors(context.Repo, text, cancellationToken).ConfigureAwait(false);
        return query.IsError ? query.Error : Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["contextQuery"] = query.Value });
    }
}
