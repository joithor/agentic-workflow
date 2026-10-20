using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The plain sentence of every block this build ships (setup-once spec §5.3.1): what the approval and the plan show for a capability before any machine id.
/// A block without a sentence fails the shipped-catalog test, so a new block cannot ship without words. A sentence is one line in plain words (no id,
/// no code name), at most 120 characters; the sentence is not part of the descriptor hash.
/// </summary>
internal sealed class ShippedBlockSentences : IBlockSentences
{
    private static readonly Dictionary<string, string> _byId = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string? For(BlockId id) => _byId.GetValueOrDefault(id.Value);

    /// <summary>The ids that have a sentence, for the test that finds a stale entry.</summary>
    internal static IReadOnlyCollection<string> Ids => _byId.Keys;
}
