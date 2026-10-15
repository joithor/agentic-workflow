using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The views print the figures the API serves and recount nothing (critique run 5 P1; review 215 arch-1): the recounts are test support.</summary>
[Lane(TestLane.Unit)]
public sealed class ServedFiguresTests
{
    private static readonly System.Reflection.Assembly _app = typeof(InstanceHeads).Assembly;

    [Fact]
    public void The_app_has_no_child_tally_to_recount_a_groups_done_and_total() =>
        Assert.DoesNotContain(_app.GetTypes(), t => t.Name == "ChildTally");

    [Fact]
    public void The_app_has_no_row_level_open_rule() =>
        Assert.Null(typeof(InstanceHeads).GetMethod("IsOpen", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
}
