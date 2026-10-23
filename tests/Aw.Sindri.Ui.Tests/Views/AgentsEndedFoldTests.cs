using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The recently ended fold in a real window (agents spec §2, §7): collapsed under the list, then each ended row with its agent, repository, resume chip and age, and "no resume command" when there is none.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsEndedFoldTests : FrameHost
{
    private async Task<Window> Shown(Action<JsonObject>? edit = null)
    {
        var list = JsonNode.Parse(GoldenFiles.Api("sessions", "recently-ended"))!;
        edit?.Invoke(list["recentlyEnded"]![0]!.AsObject());
        Server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.HasEnded);
        return window;
    }

    private static List<EndedSessionRow> Rows(Window window) => [.. window.GetVisualDescendants().OfType<EndedSessionRow>().Where(r => r.IsEffectivelyVisible)];

    private static void Toggle(Window window)
    {
        Named<Button>(window, "AgentsEndedToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
    }

    [Fact]
    public Task The_fold_is_closed_until_it_is_asked_for_and_then_lists_the_ended_row_in_full() => OnUi(async () =>
    {
        var window = await Shown();

        Assert.Empty(Rows(window));
        Assert.Equal("Recently ended (1)", Named<Button>(window, "AgentsEndedToggle").GetVisualDescendants().OfType<TextBlock>().Single().Text);

        Toggle(window);
        var row = Assert.Single(Rows(window));
        Assert.Equal("Ended", Named<TextBlock>(row, "EndedStatus").Text);
        Assert.Equal("api", Named<TextBlock>(row, "EndedRepo").Text);
        Assert.True(Named<Button>(row, "EndedCopy").IsEnabled);
        Assert.StartsWith("ended ", Named<TextBlock>(row, "EndedAgo").Text, StringComparison.Ordinal);
    });

    [Fact]
    public Task An_ended_session_with_no_resume_command_says_so_and_has_nothing_to_copy() => OnUi(async () =>
    {
        var window = await Shown(ended => ended["resume"] = null);

        Toggle(window);
        var row = Assert.Single(Rows(window));
        Assert.Equal("no resume command", Named<TextBlock>(row, "EndedResume").Text);
        Assert.False(Named<Button>(row, "EndedCopy").IsEnabled);
    });
}
