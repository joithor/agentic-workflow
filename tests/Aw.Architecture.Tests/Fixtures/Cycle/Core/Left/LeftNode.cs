using Aw.Architecture.Tests.Fixtures.Cycle.Core.Right;

namespace Aw.Architecture.Tests.Fixtures.Cycle.Core.Left;

internal sealed class LeftNode
{
    public static int Size => RightNode.Count + 1;
}
