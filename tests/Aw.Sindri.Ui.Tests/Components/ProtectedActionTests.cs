using System.ComponentModel;
using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Components;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Components;

/// <summary>
/// The lock button's decisions (screens spec fixture "The protected-action label", rev 8 critique run 5 P1 "Protected
/// actions with no path to the command"): the label, the title, the served CLI and its Copy, and what the popover never does.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ProtectedActionTests
{
    private const string Subject = "REQ-01JD4";
    private const string Cli = "sindri decision accept REQ-01JD4";
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProtectedActionViewModel Make(string verb = "Accept", string cli = Cli) => new(new ProtectedAction(verb, cli, Subject), _clipboard, _clock);

    [Theory]
    [InlineData("Grant rounds", "⌂ Grant rounds · terminal")]
    [InlineData("Accept", "⌂ Accept · terminal")]
    [InlineData("Approve", "⌂ Approve · terminal")]
    [InlineData("Re-approve", "⌂ Re-approve · terminal")]
    [InlineData("Promote", "⌂ Promote · terminal")]
    public void Label_is_lock_verb_dot_terminal(string verb, string label) => Assert.Equal(label, Make(verb).Label);

    [Fact]
    public void Title_is_Needs_a_typed_confirmation_at_a_terminal() =>
        Assert.Equal("Needs a typed confirmation at a terminal", Make().Title);

    [Fact]
    public void The_popover_shows_the_served_cli_with_Copy_and_Open_in_an_agent_and_clicking_toggles_it()
    {
        var vm = Make();

        Assert.False(vm.IsOpen);
        vm.SetOpen(open: true);

        Assert.True(vm.IsOpen);
        Assert.Equal(Cli, vm.Cli);
        Assert.Equal("Copy", vm.CopyLabel);
        Assert.Equal("Open in an agent", vm.OpenInAgentLabel);
        vm.SetOpen(open: false);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Open_in_an_agent_is_pending_with_its_phase_until_the_spec_defines_it()
    {
        var vm = Make();

        Assert.True(vm.IsOpenInAgentPending);
        Assert.Equal("Not available in the app yet", vm.OpenInAgentChip);
    }

    [Fact]
    public void Names_carry_the_subject()
    {
        var vm = Make();

        Assert.Equal($"Accept {Subject}", vm.ButtonName);
        Assert.Equal($"Copy: {Cli}", vm.CopyName);
        Assert.Equal($"Open in an agent: {Subject}, not available in the app yet", vm.OpenInAgentName);
    }

    [Fact]
    public async Task Copy_puts_the_served_cli_on_the_clipboard_and_says_Copied_for_1_5_s()
    {
        var vm = Make();

        await vm.Copy(Token);

        Assert.Equal(Cli, _clipboard.Text);
        Assert.Equal(("Copied", "Copied", true), (vm.CopyLabel, vm.Announcement, vm.IsAnnouncing));
        Assert.Equal($"Copy: {Cli}", vm.CopyName);   // the button's name stays put: the live region alone says Copied
        _clock.Advance(TimeSpan.FromMilliseconds(1499));
        Assert.Equal("Copied", vm.CopyLabel);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(("Copy", false), (vm.CopyLabel, vm.IsAnnouncing));
        Assert.Equal(vm.Label, vm.Announcement);   // a-11y-3: it rests on its label, not on the stale "Copied"
    }

    // Flag first, then text, on the way out too: the region goes dark before "Copied" is replaced by the resting text.
    [Fact]
    public async Task The_resting_text_replaces_Copied_only_after_the_flag_goes_off()
    {
        var vm = Make();
        await vm.Copy(Token);
        var seen = new List<(string Property, bool Flag, string Text)>();
        vm.PropertyChanged += (_, e) => seen.Add((e.PropertyName ?? string.Empty, vm.IsAnnouncing, vm.Announcement));

        _clock.Advance(ProtectedActionViewModel.CopiedFor);

        var text = seen.FindIndex(s => string.Equals(s.Property, nameof(vm.Announcement), StringComparison.Ordinal));
        Assert.True(text >= 0);
        Assert.False(seen[text].Flag);   // already dark when the text changes
        Assert.All(seen, s => Assert.False(string.IsNullOrEmpty(s.Text)));
    }

    // A second Copy inside the window restarts it: the first timer must not clear the second "Copied".
    [Fact]
    public async Task A_second_Copy_restarts_the_Copied_window()
    {
        var vm = Make();
        await vm.Copy(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(1000));
        await vm.Copy(Token);

        _clock.Advance(TimeSpan.FromMilliseconds(500));   // 1.5 s after the first Copy: its timer fires, and is stale
        Assert.Equal(("Copied", true), (vm.CopyLabel, vm.IsAnnouncing));
        _clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal("Copied", vm.CopyLabel);
        _clock.Advance(TimeSpan.FromMilliseconds(1));   // 1.5 s after the second
        Assert.Equal(("Copy", false), (vm.CopyLabel, vm.IsAnnouncing));
    }

    [Fact]
    public async Task The_Copied_announcement_is_flag_then_text_and_never_empty()
    {
        var vm = Make();
        var seen = new List<(string Property, bool Flag, string Text)>();
        vm.PropertyChanged += (_, e) => seen.Add((e.PropertyName ?? string.Empty, vm.IsAnnouncing, vm.Announcement));

        await vm.Copy(Token);
        await vm.Copy(Token);   // the same text again is still a change: dark first, then the resting text

        Assert.All(seen, s => Assert.False(string.IsNullOrEmpty(s.Text)));
        var firstFlag = seen.FindIndex(s => string.Equals(s.Property, nameof(vm.IsAnnouncing), StringComparison.Ordinal));
        var firstText = seen.FindIndex(s => string.Equals(s.Property, nameof(vm.Announcement), StringComparison.Ordinal) && string.Equals(s.Text, "Copied", StringComparison.Ordinal));
        Assert.InRange(firstFlag, 0, firstText - 1);   // the flag turned on before the text changed to "Copied"
        Assert.Equal("Copied", vm.Announcement);
    }

    [Fact]
    public async Task A_cli_with_a_control_character_is_neither_shown_nor_copied()
    {
        var vm = Make(cli: "sindri decision accept‮ REQ");

        await vm.Copy(Token);

        Assert.Equal((string.Empty, false), (vm.Cli, vm.CanCopy));
        Assert.Null(_clipboard.Text);
    }

    // a11y-0: one resting text. The region starts on the label, as it rests on it after "Copied", never on "Copy" (the button's own word).
    [Fact]
    public void The_announcement_starts_on_the_label_like_it_rests_on_it()
    {
        var vm = Make();

        Assert.Equal((vm.Label, false), (vm.Announcement, vm.IsAnnouncing));
    }

    // a11y-1: with no command to show, the popover says so and Copy is named for what it lacks.
    [Fact]
    public void A_cli_that_cannot_be_shown_says_so_and_Copy_is_named_for_it()
    {
        var vm = Make(cli: "sindri decision accept‮ REQ");

        Assert.Equal("The served command can't be shown", vm.CliText);
        Assert.Equal("Copy: no command served", vm.CopyName);
        Assert.Equal(Cli, Make().CliText);
    }

    [Fact]
    public async Task A_refused_clipboard_says_nothing_copied()
    {
        _clipboard.Refuse = true;
        var vm = Make();

        await vm.Copy(Token);

        Assert.Equal(("Copy", false), (vm.CopyLabel, vm.IsAnnouncing));
    }

    // The popover only shows the line and copies it: the view model has no way to send (no API, no sender, no delegate).
    [Fact]
    public void It_never_sends_a_command()
    {
        var parameters = typeof(ProtectedActionViewModel).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();

        Assert.All(parameters, t => Assert.Contains(t, new[] { typeof(ProtectedAction), typeof(IClipboard), typeof(TimeProvider) }));
        Assert.DoesNotContain(typeof(ProtectedActionViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            f => typeof(Delegate).IsAssignableFrom(f.FieldType) || f.FieldType.Name.Contains("Api", StringComparison.Ordinal));
        Assert.All(typeof(ProtectedActionViewModel).GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            e => Assert.Equal(typeof(PropertyChangedEventHandler), e.EventHandlerType));
    }
}
