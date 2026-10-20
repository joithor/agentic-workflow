using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's, Agents page's and Context page's words (owner feedback F3/F4): a user who has never read the spec understands each one.
/// These pin the words so a rename back to an internal term fails here.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PlainWordsTests
{
    [Fact]
    public void The_navigation_and_the_unserved_words_are_plain() =>
        Assert.Equal(("Workflow types", "coming soon", "coming soon", "Refreshing the lists", "Lists are up to date"),
            (Labels.Recipes, Labels.NotServedYet, Labels.NotInApp, Labels.ViewsRebuilding, Labels.ViewsRecovered));

    [Fact]
    public void The_agents_page_words_are_plain() =>
        Assert.Equal(
            ("Not part of a workflow", "Agent session", "Any status", "Watch the agent", "Coming soon", "3 agents · 1 asking you", "1 agent · 0 asking you", "Lane sindri/ideas"),
            (Labels.OutsideWorkflow, Labels.LaunchLine, Labels.AllStates, Labels.FocusLabel, Labels.FocusSoon, Labels.AgentsHead(3, 1), Labels.AgentsHead(1, 0), Labels.LaneLine("sindri", "ideas")));

    [Fact]
    public void The_session_sheet_labels_are_plain() =>
        Assert.Equal(
            ["Agent session", "Run", "Step", "Open the run", "In this status since", "How this was detected", "History", "Details"],
            [Labels.SheetLaunch, Labels.SheetInstance, Labels.SheetStep, Labels.OpenStepFrame, Labels.SheetSince, Labels.SheetStateSource, Labels.SheetHistory, Labels.SheetDetails]);

    [Fact]
    public void The_context_page_words_are_plain() =>
        Assert.Equal(
            ["Look up", "Type what to look up, such as feature:name", "No context map yet", "1 topic", "4 topics", "Why", "What it covers", "Links", "How well it is backed", "What was searched", "More about this answer"],
            [Labels.ContextAnchor, Labels.ContextAnchorHint, Labels.ContextNoMap, Labels.ContextHead(1), Labels.ContextHead(4), Labels.ContextIntent, Labels.ContextProduct, Labels.ContextEdges, Labels.ContextCoverage, Labels.ContextSearched, Labels.ContextMore]);

    [Fact]
    public void The_shortcut_sheet_names_pages_and_actions_in_plain_words()
    {
        var go = Labels.FixedKeys.Single(k => k.Keys.StartsWith("G then", StringComparison.Ordinal));

        Assert.Equal("Go to a page: H Home, W Workflows, Q Work, R Workflow types, C Context, A Agents, . System", go.Action);
        Assert.Contains(("O", "Open the linked page"), Labels.FixedKeys);
        Assert.Contains(("Y", "Copy the row's command"), Labels.FixedKeys);
        Assert.Contains(("X / Shift+X", "Decline / cancel the run"), Labels.FixedKeys);
        Assert.Contains(("F6 / Shift+F6", "Next / previous area"), Labels.FixedKeys);
    }
}
