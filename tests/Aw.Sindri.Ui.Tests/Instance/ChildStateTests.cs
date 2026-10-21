using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A child's served state (engine P7): "unreachable" is its own case with a warning mark and the word, any other state is plain text; answers forwarded to a child never move the parent's other commands.</summary>
[Lane(TestLane.Unit)]
public sealed class ChildStateTests
{
    private const string Mark = "⚠";

    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ExpandedStateViewModel Of(StatusFields status) =>
        new(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock);

    [Fact]
    public void An_unreachable_child_is_drawn_with_a_warning_mark_and_the_word()
    {
        var status = FixtureInstances.Status("unreachable-child");

        var pointer = Of(status).Child!;

        Assert.Equal("unreachable", status.Child!.State);
        Assert.Contains($"{Mark} unreachable", pointer.Text, StringComparison.Ordinal);
        Assert.Contains(status.Child.Address, pointer.Text, StringComparison.Ordinal);
        Assert.StartsWith(pointer.Text, pointer.Name, StringComparison.Ordinal);   // the name leads with the drawn words
    }

    [Fact]
    public void Any_other_child_state_is_plain_text_with_its_reason()
    {
        var status = FixtureInstances.Status("parked-child");

        var pointer = Of(status).Child!;

        Assert.Equal($"Child run {status.Child!.Address} · parked: no-progress", pointer.Text);
        Assert.DoesNotContain(Mark, pointer.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("Unreachable")]
    [InlineData("review.unreachable")]
    public void Only_the_exact_word_is_the_unreachable_case(string state) => Assert.Equal(state, Labels.ChildStateWords(state));

    [Fact]
    public void A_child_row_draws_an_unreachable_state_as_its_own_case()
    {
        var served = FixtureInstances.ChildRows(FixtureInstances.Detail("project-many-children").Children[0]).First();
        var unreachable = new ChildRowViewModel(served with { State = "unreachable" }, _clipboard, _clock);
        var plain = new ChildRowViewModel(served with { State = "building" }, _clipboard, _clock);

        Assert.Contains($"{Mark} unreachable", unreachable.Signature, StringComparison.Ordinal);
        Assert.Contains("building", plain.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain(Mark, plain.Signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unreachable-child")]
    [InlineData("parked-child")]
    public void Cancel_takeover_and_handback_stay_on_the_parent_and_the_UI_sends_none_to_the_child(string scenario)
    {
        var status = FixtureInstances.Status(scenario);

        var expanded = Of(status);

        var others = status.Commands.Where(c => c.Verb is StatusVerb.Cancel or StatusVerb.Takeover or StatusVerb.Handback).ToList();
        Assert.NotEmpty(others);
        Assert.All(others, c =>
        {
            Assert.Contains(status.Header.Address, c.Cli, StringComparison.Ordinal);
            Assert.DoesNotContain(status.Child!.Address, c.Cli, StringComparison.Ordinal);
        });
        Assert.All(expanded.Commands.Where(c => c.Verb is StatusVerb.Cancel or StatusVerb.Takeover or StatusVerb.Handback), c => Assert.DoesNotContain(status.Child!.Address, c.Cli, StringComparison.Ordinal));
        Assert.Null(typeof(ExpandedStateViewModel).GetMethods().FirstOrDefault(m => m.Name.Contains("Send", StringComparison.Ordinal)));   // the app sends no command at all (U8b)
    }
}
