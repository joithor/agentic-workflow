using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// 'step.lens.round', on the block runtime (line spec §5.2, LN-PK lite): resolves the design lens pack the approved profile chooses, queues the lenses that apply to the scope's surface kinds
/// ('when'), and records each other lens as 'notApplicable' in 'fact:lensRounds' with the reason. 'ready'; a pack none of whose lenses apply fails the Step (a design review of nothing is not a pass).
/// A scope that lists no surfaces at all, or that the fact doesn't hold in a known shape, selects every lens.
/// </summary>
/// <param name="packs">Resolves the pack.</param>
internal sealed class LensRoundStep(ILensPacks packs) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.lens.round");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var (rounds, reason) = RoundSeed.Log(context);
        if (rounds is null)
        {
            return BlockStepResult.Failed($"step.lens.round: {reason}");
        }

        var pack = await packs.Resolve(LensPackKind.Design, context.Repo, cancellationToken).ConfigureAwait(false);
        if (pack.IsError)
        {
            return BlockStepResult.Failed($"step.lens.round can't resolve the design lens pack: {pack.Error.Message}");
        }

        var selection = LensSelection.Of(pack.Value, SurfaceKinds(StepFacts.Read(context, "fact:scope")), changedPaths: null);
        return selection.Queued.Count == 0
            ? BlockStepResult.Failed($"step.lens.round: no lens of the pack '{pack.Value.Id.Value}' applies to this scope ({string.Join("; ", selection.NotApplicable.Select(static n => $"{n.Lens.Value}: {n.Reason}"))})")
            : RoundSeed.Ready(pack.Value, selection, rounds);
    }

    // The kinds of the scope map's surfaces, in either place the intake may put the map; null when the fact holds no surface list.
    private static HashSet<string>? SurfaceKinds(JsonNode? scope)
    {
        var surfaces = (scope as JsonObject)?["surfaces"] as JsonArray ?? ((scope as JsonObject)?["scopeMap"] as JsonObject)?["surfaces"] as JsonArray;
        return surfaces is null ? null : new HashSet<string>(surfaces.Select(static s => LoopJson.Str(s, "kind")).OfType<string>(), StringComparer.OrdinalIgnoreCase);
    }
}
