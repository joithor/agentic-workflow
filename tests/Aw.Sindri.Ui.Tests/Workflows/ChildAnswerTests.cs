using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A row whose answer a child instance decides (engine #298: answer.via is "child"): it says "Answer in the child" and follows it; it never offers the engine's placeholder as an answer.</summary>
[Lane(TestLane.Unit)]
public sealed class ChildAnswerTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static readonly string[] _pointers = ["escalated-child", "parked-mirrored-budget"];

    private StripRowViewModel Strip(InstanceSummary row) => new(row, _clipboard, _clock);

    private InstanceRowViewModel Listed(InstanceSummary row) => new(row, _clipboard, _clock);

    public static TheoryData<string> Pointers => [.. _pointers];

    private static InstanceSummary WithoutChild(InstanceSummary row) => row with { Answer = row.Answer! with { Child = null } };

    [Theory]
    [MemberData(nameof(Pointers))]
    public void A_strip_row_that_points_at_a_child_says_so_and_has_no_answer_button_lock_or_copy(string scenario)
    {
        var served = FixtureInstances.Summary(scenario);

        var vm = Strip(served);

        Assert.Equal(Labels.AnswerInChild, vm.ChildAnswer!.Text);
        Assert.Equal(Labels.AnswerInChildName(served.Answer!.Child!), vm.ChildAnswer.Name);
        Assert.False(vm.CanAnswer);
        Assert.Null(vm.Protected);
        Assert.Null(_clipboard.Text);
    }

    [Theory]
    [MemberData(nameof(Pointers))]
    public void A_list_row_that_points_at_a_child_says_so_and_has_no_lock_or_desk_answer(string scenario)
    {
        var vm = Listed(FixtureInstances.Summary(scenario));

        Assert.Equal(Labels.AnswerInChild, vm.ChildAnswer!.Text);
        Assert.Null(vm.Protected);
        Assert.False(vm.CanAnswerAtDesk);
    }

    [Theory]
    [MemberData(nameof(Pointers))]
    public void A_pointer_with_a_child_opens_that_childs_frame_and_asks_for_its_first_read_action(string scenario)
    {
        var served = FixtureInstances.Summary(scenario);

        var strip = Strip(served).ChildAnswer!;
        var listed = Listed(served).ChildAnswer!;

        foreach (var answer in (ChildAnswerViewModel[])[strip, listed])
        {
            Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Answer!.Child), answer.Link);
            Assert.Equal(FrameIntent.OpenInitial, answer.Intent);
            Assert.True(answer.OpensChild);
        }
    }

    [Theory]
    [MemberData(nameof(Pointers))]
    public void A_pointer_without_a_child_opens_the_parents_frame_at_its_children_panel(string scenario)
    {
        var served = WithoutChild(FixtureInstances.Summary(scenario));

        foreach (var answer in (ChildAnswerViewModel[])[Strip(served).ChildAnswer!, Listed(served).ChildAnswer!])
        {
            Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Address), answer.Link);
            Assert.Equal(FrameIntent.FocusChildren, answer.Intent);
            Assert.False(answer.OpensChild);
            Assert.Equal(Labels.AnswerInChildParentName(served.Address), answer.Name);   // it says it opens the parent's children, not that it answers at an address that is not the child's
            Assert.Contains("children", answer.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_pointer_is_read_from_via_and_never_from_the_cli_text()
    {
        var served = FixtureInstances.Summary("escalated-child");

        var noVia = served with { Answer = served.Answer! with { Via = null } };
        var otherVia = served with { Answer = served.Answer! with { Via = "somewhere" } };

        Assert.All((InstanceSummary[])[noVia, otherVia], row =>
        {
            Assert.Contains("answer", row.Answer!.Cli, StringComparison.Ordinal);   // the placeholder text is still there: it decides nothing
            Assert.Null(Strip(row).ChildAnswer);
            Assert.Null(Listed(row).ChildAnswer);
            Assert.True(Strip(row).CanAnswer);
        });
    }

    [Fact]
    public void Rows_that_are_not_pointers_are_unchanged()
    {
        Assert.All(FixtureInstances.Rows.Where(r => !string.Equals(r.Answer?.Via, "child", StringComparison.Ordinal)), row =>
        {
            Assert.Null(Strip(row).ChildAnswer);
            Assert.Null(Listed(row).ChildAnswer);
            Assert.Equal(InstanceRules.HasProtectedAnswer(row), Strip(row).Protected is not null);
            Assert.Equal(InstanceRules.HasProtectedAnswer(row), Listed(row).Protected is not null);
        });
    }

    [Fact]
    public void A_pointer_row_signature_differs_from_the_row_that_follows_another_child()
    {
        var served = FixtureInstances.Summary("escalated-child");

        Assert.NotEqual(Strip(served).Signature, Strip(WithoutChild(served)).Signature, StringComparer.Ordinal);
        Assert.NotEqual(Listed(served).Signature, Listed(WithoutChild(served)).Signature, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pointers))]
    public void A_pointer_whose_served_class_is_protected_still_shows_no_lock(string scenario)
    {
        // An in-memory copy of the pointer golden with class "protected": the lock must stay off, or the row would offer a protected answer the child decides.
        var served = FixtureInstances.Summary(scenario);
        var protectedPointer = served with { Answer = served.Answer! with { Class = "protected" } };

        Assert.False(InstanceRules.HasProtectedAnswer(protectedPointer));
        Assert.Null(Strip(protectedPointer).Protected);
        Assert.Null(Listed(protectedPointer).Protected);
        Assert.NotNull(Strip(protectedPointer).ChildAnswer);
    }
}
