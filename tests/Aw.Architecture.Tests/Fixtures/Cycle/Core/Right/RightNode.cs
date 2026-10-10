using Aw.Architecture.Tests.Fixtures.Cycle.Core.Left;

namespace Aw.Architecture.Tests.Fixtures.Cycle.Core.Right;

internal sealed class RightNode
{
    public static int Count => 2;

    public static int Both => LeftNode.Size + Count;
}
