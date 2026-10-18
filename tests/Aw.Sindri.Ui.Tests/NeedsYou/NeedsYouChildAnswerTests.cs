using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A needs-you row that carries via: "child": it says "Answer in the child", offers no answer form and keeps the engine's placeholder off every copy path.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouChildAnswerTests
{
    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    internal static NeedsYouRowViewModel Row(string? via)
    {
        var list = Golden<NeedsYouList>("needs-you", "park");
        var served = list with { Rows = [list.Rows[0] with { Via = via }] };
        var entry = NeedsYouMapper.Map(served, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0)).Rows[0];
        return new NeedsYouRowViewModel(entry, seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
    }

    [Fact]
    public void A_child_pointer_row_says_Answer_in_the_child_and_does_not_carry_the_placeholder()
    {
        var row = Row("child");

        Assert.Equal(Labels.AnswerInChild, row.ChildAnswer!.Text);
        Assert.Equal(Labels.AnswerInChildName(row.ShownId), row.ChildAnswer.Name);
        Assert.Equal(row.Id, row.ChildAnswer.NeedsYouRowId);
        Assert.Empty(row.Cli);
    }

    [Fact]
    public void A_child_pointer_row_is_mapped_without_the_placeholder_action_so_no_answer_control_is_built()
    {
        var served = Golden<NeedsYouList>("needs-you", "park").Rows[0];
        Assert.NotEmpty(served.Actions);   // the engine does serve a placeholder action on it: the row must drop it

        var row = Row("child");

        Assert.Empty(row.Controls);
        Assert.Empty(row.Entry.Actions);
        Assert.Empty(row.Entry.PrimaryAction);
    }

    [Fact]
    public void A_row_without_via_is_unchanged()
    {
        var served = Golden<NeedsYouList>("needs-you", "park").Rows[0];

        var row = Row(via: null);

        Assert.Null(row.ChildAnswer);
        Assert.Equal(served.Cli, row.Cli);
        Assert.NotEmpty(row.Controls);
    }
}
