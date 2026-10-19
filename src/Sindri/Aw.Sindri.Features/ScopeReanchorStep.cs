using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.reanchor', on the block runtime: <see cref="ScopingSteps.Reanchor"/>'s code over the changed map. The map's own node keys and the
/// anchors already held always count; the context engine's text shape over the surfaces' titles adds suggestions when it answers, and a map it cannot
/// answer for (no repo, no profile, an engine error) is left out, as the desk leaves out a missing 'contextQuery'.
/// </summary>
/// <param name="shape">The engine's text shape.</param>
internal sealed class ScopeReanchorStep(IScopingTextShape shape) : ScopingCodeStep(Descriptor), IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Reanchor;

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var titles = ScopeJson.Items(Fact(context, "scopeMap"), "surfaces").Select(static s => ScopeJson.Str(s, "title")).OfType<string>();
        var query = await shape.Anchors(context.Repo, string.Join('\n', titles), cancellationToken).ConfigureAwait(false);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!query.IsError)
        {
            facts["contextQuery"] = query.Value;
        }

        return Facts(facts);
    }
}
