using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Review;

/// <summary>Orders node keys by their text, ordinally.</summary>
internal sealed class KeyOrder : IComparer<NodeKey>
{
    internal static KeyOrder Instance { get; } = new();

    public int Compare(NodeKey x, NodeKey y) => string.CompareOrdinal(x.Value, y.Value);
}
