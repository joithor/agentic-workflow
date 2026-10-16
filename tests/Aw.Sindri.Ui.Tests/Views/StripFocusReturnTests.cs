using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Closing a sheet opened from a strip row returns focus to that strip row (review run 1, a11y-13; UI spec §11.2 "Esc or back returns it to the row").</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StripFocusReturnTests : FrameHost
{
    [Fact]
    public Task Esc_after_Answer_returns_focus_to_the_strip_row_not_the_Needs_you_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Strip.Rows.Count > 0);
        var row = shell.Strip.Rows.First(r => r.CanAnswer);
        var park = NeedsYouRows.Park("park:" + row.Address) with { Subject = new NeedsYouSubject("workflow", row.Address) };
        shell.ShowNeedsYou(new NeedsYouFeed([park], ""));
        Settle(window);
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "StripRowFrame" && b.DataContext is StripRowViewModel vm && vm.Address == row.Address);
        Assert.True(frame.Focus(NavigationMethod.Tab));
        Settle(window);

        shell.AnswerStripRow(row.Address);
        Settle(window);
        shell.CloseSheet();
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Same(frame, window.FocusManager?.GetFocusedElement());
    });
}
