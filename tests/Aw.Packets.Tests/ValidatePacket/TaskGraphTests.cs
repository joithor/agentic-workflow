using System.Globalization;
using Aw.Packets.Core.Checks;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

public sealed class TaskGraphTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_dependency_chain_of_50_000_tasks_is_walked_without_recursion()
    {
        var tasks = Enumerable.Range(0, 50_000)
            .Select(static i => A.Task("t" + i.ToString(CultureInfo.InvariantCulture))
                .After("t" + (i == 0 ? 49_999 : i - 1).ToString(CultureInfo.InvariantCulture)).Build())
            .ToList();

        var cycle = Assert.Single(new TaskGraph(tasks).Cycles());

        Assert.Equal(50_000, cycle.Members.Count);
        Assert.Equal(50_001, cycle.Path.Count);
    }
}
