using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.Tests.Components;

/// <summary>
/// The one Attach component's decisions (screens spec §4.5 action sets, rev 8 critique run 5 "Attach has four shapes"):
/// the main action by Step kind, the chevron's entries, the pending chip, and the Cancel that asks first. Addresses
/// are opaque strings in the engine's two shapes and are never parsed.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AttachControlTests
{
    private const string Project = "ITM-01JE7/project/1";
    private const string Item = "ITM-01JE9/item.4";

    private static void Same(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal), string.Join(" | ", actual));

    private static AttachControlViewModel Agent(string address = Project, bool ready = true, string? confirm = null) =>
        new(new AttachInput(address, AttachStepKind.Agent, ready, confirm));

    [Theory]
    [InlineData(Project)]
    [InlineData(Item)]
    public void Main_is_Attach_and_the_chevron_holds_Take_over_File_a_request_Cancel(string address)
    {
        var vm = Agent(address);

        Assert.True(vm.IsPrimaryShown);
        Assert.Equal("Watch the agent", vm.PrimaryLabel);
        Assert.Equal($"Watch the agent for {address}", vm.PrimaryName);
        Assert.Equal(AttachAction.Attach, vm.PrimaryAction);
        Same(["Take over", "File a request", "Cancel"], vm.Menu.Select(m => m.Label));
        Same([$"Take over {address}", $"File a request for {address}", $"Cancel {address}"], vm.Menu.Select(m => m.Name));
        Assert.Equal($"More actions for {address}", vm.ChevronName);
    }

    [Theory]
    [InlineData(Project)]
    [InlineData(Item)]
    public void The_attach_note_reads_the_fixture_sentence_under_every_Attach_primary(string address)
    {
        var vm = Agent(address);

        Assert.True(vm.HasNote);
        Assert.Equal("Watch the agent's terminal live. Typing takes it over.", vm.Note);
    }

    [Fact]
    public void A_served_reason_replaces_the_P10a_words_in_the_chip_and_its_name()
    {
        var vm = new AttachControlViewModel(new AttachInput(Project, AttachStepKind.Agent, AttachReady: false, PendingReason: "The agent isn't running, so there's nothing to watch"));

        Assert.Equal("The agent isn't running, so there's nothing to watch", vm.PendingChip);
        Assert.Equal($"Watch the agent for {Project}: the agent isn't running, so there's nothing to watch", vm.PendingChipName);
        vm.Update(new AttachInput(Project, AttachStepKind.Agent, AttachReady: false, PendingReason: "The agent session is still starting"));
        Assert.Equal("The agent session is still starting", vm.PendingChip);
    }

    [Fact]
    public void Attach_is_pending_P10a_until_the_catalog_says_ready()
    {
        var vm = Agent(ready: false);
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);

        Assert.True(vm.IsPending);
        Assert.False(vm.IsPrimaryShown);
        Assert.False(vm.HasNote);
        Assert.Equal("Not available in the app yet", vm.PendingChip);
        vm.Choose(AttachAction.Attach);
        Assert.Empty(raised);

        vm.Update(new AttachInput(Project, AttachStepKind.Agent, AttachReady: true));

        Assert.False(vm.IsPending);
        Assert.True(vm.IsPrimaryShown);
        Assert.True(vm.HasNote);
        vm.Choose(AttachAction.Attach);
        Assert.Equal([AttachAction.Attach], raised);
    }

    [Fact]
    public void Cancel_in_the_chevron_asks_first()
    {
        var vm = Agent();
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);

        vm.Choose(AttachAction.Cancel);

        Assert.True(vm.IsConfirmingCancel);
        Assert.Equal($"Cancel {Project}?", vm.ConfirmLine);
        Assert.Equal($"Cancel it: {Project}", vm.ConfirmButtonName);   // starts with the visible label (WCAG 2.5.3)
        Assert.Equal($"Keep {Project}", vm.KeepButtonName);
        Assert.Empty(raised);

        vm.ConfirmCancel();

        Assert.False(vm.IsConfirmingCancel);
        Assert.Equal([AttachAction.Cancel], raised);
    }

    [Fact]
    public void Cancel_asks_the_served_sentence_when_there_is_one_and_Keep_asks_nothing_further()
    {
        const string served = "Cancel ITM-01JE9/item.4? 2 running steps stop; 1 children are cancelled.";
        var vm = Agent(Item, confirm: served);
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);

        vm.Choose(AttachAction.Cancel);
        Assert.Equal(served, vm.ConfirmLine);
        vm.Keep();

        Assert.False(vm.IsConfirmingCancel);
        Assert.Empty(raised);
    }

    [Fact]
    public void ConfirmCancel_with_no_confirm_standing_raises_nothing()
    {
        var vm = Agent();
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);

        vm.ConfirmCancel();

        Assert.Empty(raised);
        Assert.False(vm.IsConfirmingCancel);
    }

    [Fact]
    public void An_update_during_the_confirm_keeps_it_and_takes_the_new_served_sentence()
    {
        var vm = Agent();
        vm.Choose(AttachAction.Cancel);

        vm.Update(new AttachInput(Project, AttachStepKind.Agent, AttachReady: true, "Cancel it? 3 steps stop."));

        Assert.True(vm.IsConfirmingCancel);
        Assert.Equal("Cancel it? 3 steps stop.", vm.ConfirmLine);
        Assert.Equal("Cancel it? 3 steps stop.", vm.ConfirmAnnouncement);
    }

    // A confirm about one Step must never go ahead against another: the component was handed a different address.
    [Fact]
    public void An_update_with_a_different_address_closes_the_confirm_and_a_later_confirm_raises_nothing()
    {
        var vm = Agent();
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);
        vm.Choose(AttachAction.Cancel);

        vm.Update(new AttachInput(Item, AttachStepKind.Agent, AttachReady: true));

        Assert.False(vm.IsConfirmingCancel);
        Assert.False(vm.IsConfirmLive);
        Assert.False(string.IsNullOrEmpty(vm.ConfirmAnnouncement));
        vm.ConfirmCancel();
        Assert.Empty(raised);
    }

    [Fact]
    public void The_announcement_region_joins_the_tree_only_from_the_menu_opening()
    {
        var vm = Agent();

        Assert.False(vm.IsRegionInTree);
        vm.MenuOpened();
        Assert.True(vm.IsRegionInTree);
    }

    [Fact]
    public void Take_over_and_File_a_request_go_straight_through()
    {
        var vm = Agent();
        var raised = new List<AttachAction>();
        vm.Chosen += (_, e) => raised.Add(e.Action);

        vm.Choose(AttachAction.TakeOver);
        vm.Choose(AttachAction.FileRequest);

        Assert.Equal([AttachAction.TakeOver, AttachAction.FileRequest], raised);
        Assert.False(vm.IsConfirmingCancel);
    }

    // Flag then text, never "" (docs/csharp/sindri-ui.md "LiveSetting on macOS"): the announcement is set while the
    // region is live, and its resting text (never empty) only while it is off.
    [Fact]
    public void The_confirm_announcement_flips_its_flag_before_its_text_and_is_never_empty()
    {
        var vm = Agent();
        var changes = new List<string>();
        var texts = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            changes.Add(e.PropertyName ?? "");
            if (e.PropertyName == nameof(AttachControlViewModel.ConfirmAnnouncement))
            {
                texts.Add(vm.ConfirmAnnouncement);
            }
        };
        Assert.False(string.IsNullOrEmpty(vm.ConfirmAnnouncement));
        Assert.False(vm.IsConfirmLive);

        vm.Choose(AttachAction.Cancel);
        Assert.True(changes.IndexOf(nameof(AttachControlViewModel.IsConfirmLive)) < changes.IndexOf(nameof(AttachControlViewModel.ConfirmAnnouncement)));
        Assert.Equal($"Cancel {Project}?", vm.ConfirmAnnouncement);
        Assert.True(vm.IsConfirmLive);

        changes.Clear();
        vm.Keep();
        Assert.True(changes.IndexOf(nameof(AttachControlViewModel.IsConfirmLive)) < changes.IndexOf(nameof(AttachControlViewModel.ConfirmAnnouncement)));
        Assert.False(vm.IsConfirmLive);
        Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.False(string.IsNullOrEmpty(vm.ConfirmAnnouncement));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Tool_and_model_steps_get_Open_instance_not_Attach(bool tool, bool ready)
    {
        var vm = new AttachControlViewModel(new AttachInput(Project, tool ? AttachStepKind.Tool : AttachStepKind.Model, ready));

        Assert.True(vm.IsPrimaryShown);
        Assert.False(vm.IsPending);
        Assert.False(vm.HasNote);
        Assert.Equal("Open run", vm.PrimaryLabel);
        Assert.Equal($"Open run {Project}", vm.PrimaryName);
        Assert.Equal(AttachAction.OpenInstance, vm.PrimaryAction);
        Same(["Take over", "Cancel"], vm.Menu.Select(m => m.Label));
    }
}
