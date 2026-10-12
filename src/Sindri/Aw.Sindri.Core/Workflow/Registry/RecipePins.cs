namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What an approval pins (spec §4.1, W11): the descriptor hash of each block the recipe names and the hash of each child, both sorted by id.</summary>
/// <param name="Blocks">The block pins.</param>
/// <param name="Children">The child pins.</param>
public sealed record RecipePins(IReadOnlyList<BlockPin> Blocks, IReadOnlyList<ChildPin> Children)
{
    /// <summary>No pins.</summary>
    public static RecipePins None { get; } = new([], []);
}
