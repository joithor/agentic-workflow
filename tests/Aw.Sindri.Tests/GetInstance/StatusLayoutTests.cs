using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// 'workflow status' layouts (engine spec §6.4): a golden file per §6.4 layout and per §3.8 row P4 can reach, rendered
/// from the view 'GetInstance' builds. Plain ASCII, the labelled lines in their fixed order.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StatusLayoutTests
{
    public static TheoryData<string> Cases => [.. StatusCases.Names];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Status_renders_the_section_6_4_layout(string name)
    {
        var text = StatusLayout.Render(StatusCases.View(name), StatusCases.Now);

        Assert.All(text, static c => Assert.True(c is '\n' or (>= ' ' and <= '~'), $"not plain ASCII: '{c}'"));
        Golden.Match(text, typeof(StatusLayoutTests).Assembly, $"GetInstance/Golden/{name}.txt", Normalization.None);
    }
}
