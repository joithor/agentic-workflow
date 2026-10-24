using Aw.UiDriver.Core.Protocol;
using Aw.UiDriver.Core.Selectors;

namespace Aw.UiDriver.Tests;

public sealed class SelectorTests
{
    private static readonly NodeFacts[] _tree =
    [
        new("0", "Window", null, "Main", null, true, true),
        new("0/0", "Button", "save", null, "Save", true, true),
        new("0/1", "Button", "cancel", null, "Cancel", true, false),
        new("0/2", "TextBlock", null, "greeting", "Hello, Ada", true, true),
        new("0/3", "Button", "save-all", null, "Save all", false, true),
    ];

    [Fact, Trait("Lane", "unit")]
    public void Every_field_given_must_match()
    {
        Assert.Equal(["0/0"], SelectorMatcher.All(new Selector(AutomationId: "save", Type: "Button"), _tree).Select(static n => n.Id));
        Assert.Empty(SelectorMatcher.All(new Selector(AutomationId: "save", Type: "TextBlock"), _tree));
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_matches_exactly_and_by_a_part()
    {
        Assert.Equal(["0/0"], SelectorMatcher.All(new Selector(Text: "Save"), _tree).Select(static n => n.Id));
        Assert.Equal(["0/0", "0/3"], SelectorMatcher.All(new Selector(TextContains: "Save"), _tree).Select(static n => n.Id));
        Assert.Equal(["0/2"], SelectorMatcher.All(new Selector(Name: "greeting"), _tree).Select(static n => n.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_index_picks_among_the_matches_in_tree_order()
    {
        Assert.Equal("0/0", SelectorMatcher.Pick(new Selector(Type: "Button"), _tree)?.Id);
        Assert.Equal("0/1", SelectorMatcher.Pick(new Selector(Type: "Button", Index: 1), _tree)?.Id);
        Assert.Equal("0/3", SelectorMatcher.Pick(new Selector(Type: "Button", Index: 2), _tree)?.Id);
        Assert.Null(SelectorMatcher.Pick(new Selector(Type: "Button", Index: 3), _tree));
        Assert.Null(SelectorMatcher.Pick(new Selector(Type: "Button", Index: -1), _tree));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_id_names_one_node()
    {
        Assert.Equal("0/2", SelectorMatcher.Pick(new Selector(NodeId: "0/2"), _tree)?.Id);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_selector_names_nothing_and_says_so()
    {
        Assert.True(new Selector().IsEmpty);
        Assert.True(new Selector(Index: 2).IsEmpty);
        Assert.False(new Selector(Name: "x").IsEmpty);
        Assert.Equal("(nothing)", new Selector().Describe());
        Assert.Equal("automationId 'save' type 'Button'", new Selector(AutomationId: "save", Type: "Button").Describe());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("exists", WaitState.Exists)]
    [InlineData("Visible", WaitState.Visible)]
    [InlineData("hidden", WaitState.Hidden)]
    [InlineData("gone", WaitState.Gone)]
    [InlineData("enabled", WaitState.Enabled)]
    [InlineData("disabled", WaitState.Disabled)]
    [InlineData("textEquals", WaitState.TextEquals)]
    [InlineData("textcontains", WaitState.TextContains)]
    [InlineData(null, WaitState.Visible)]
    public void A_wait_state_is_read_by_name_and_visible_by_default(string? name, WaitState expected)
    {
        Assert.True(WaitStates.TryParse(name, out var state));
        Assert.Equal(expected, state);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("soon")]
    [InlineData("42")]
    [InlineData("")]
    public void A_name_that_is_no_state_is_refused(string name) => Assert.False(WaitStates.TryParse(name, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_state_holds_for_the_facts_of_the_node_it_waits_on()
    {
        var shown = _tree[1];
        var disabled = _tree[2];
        var hidden = _tree[4];

        Assert.True(WaitStates.Holds(WaitState.Exists, hidden, null));
        Assert.True(WaitStates.Holds(WaitState.Visible, shown, null));
        Assert.False(WaitStates.Holds(WaitState.Visible, hidden, null));
        Assert.True(WaitStates.Holds(WaitState.Hidden, hidden, null));
        Assert.True(WaitStates.Holds(WaitState.Disabled, disabled, null));
        Assert.False(WaitStates.Holds(WaitState.Enabled, disabled, null));
        Assert.True(WaitStates.Holds(WaitState.TextEquals, shown, "Save"));
        Assert.False(WaitStates.Holds(WaitState.TextEquals, shown, "Sav"));
        Assert.True(WaitStates.Holds(WaitState.TextContains, shown, "av"));
        Assert.False(WaitStates.Holds(WaitState.TextContains, shown, null));
    }

    [Fact, Trait("Lane", "unit")]
    public void Gone_holds_only_when_nothing_matches_and_every_other_state_needs_a_node()
    {
        Assert.True(WaitStates.Holds(WaitState.Gone, null, null));
        Assert.False(WaitStates.Holds(WaitState.Gone, _tree[1], null));
        Assert.All(
            new[] { WaitState.Exists, WaitState.Visible, WaitState.Hidden, WaitState.Enabled, WaitState.Disabled, WaitState.TextEquals, WaitState.TextContains },
            state => Assert.False(WaitStates.Holds(state, null, "x")));
        Assert.True(WaitStates.NeedsText(WaitState.TextEquals));
        Assert.False(WaitStates.NeedsText(WaitState.Visible));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Enter", "Enter", false, false, false, false)]
    [InlineData("Ctrl+K", "K", true, false, false, false)]
    [InlineData("control+shift+P", "P", true, true, false, false)]
    [InlineData("Alt+F4", "F4", false, false, true, false)]
    [InlineData("Cmd+Shift+Alt+Ctrl+Z", "Z", true, true, true, true)]
    public void A_key_chord_is_the_modifiers_then_the_key(string text, string key, bool control, bool shift, bool alt, bool meta)
    {
        Assert.True(KeyChord.TryParse(text, out var chord));
        Assert.Equal(new KeyChord(key, control, shift, alt, meta), chord);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Hyper+K")]
    [InlineData("Ctrl+Ctrl+K")]
    public void A_key_chord_with_no_key_an_unknown_modifier_or_a_repeat_is_refused(string? text) => Assert.False(KeyChord.TryParse(text, out _));
}
