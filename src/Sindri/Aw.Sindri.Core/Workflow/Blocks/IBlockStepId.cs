using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block-runtime Step that names itself by id only (see AddBlockRuntime): its descriptor is registered where the catalog is, so the Step's code can live in the slice that owns it.</summary>
public interface IBlockStepId
{
    /// <summary>The Step's block id, which must have a registered descriptor whose runtime is <see cref="BlockRuntime.Block"/>.</summary>
    public static abstract BlockId Id { get; }
}
