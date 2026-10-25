using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The plain sentence of each shipped block (setup-once spec §5.3.1), kept apart from the descriptors so the descriptor hash an approval pins does not move.
/// The <see cref="BlockCatalog"/> reads it once, at construction, and every descriptor it hands out carries <see cref="BlockDescriptor.Sentence"/>.
/// </summary>
public interface IBlockSentences
{
    /// <summary>The sentence of <paramref name="id"/>.</summary>
    /// <param name="id">A block id.</param>
    /// <returns>The sentence, or null when the block has none (the shipped-catalog test refuses that).</returns>
    public string? For(BlockId id);
}
