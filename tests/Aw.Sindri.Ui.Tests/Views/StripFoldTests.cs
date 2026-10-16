using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The fold (screens §4.1): at 1280 × 800 Home's summary, Needs you, the strip and the column tops are all above the fold.</summary>
[Lane(TestLane.Integration)]
public sealed class StripFoldTests : FrameHost
{
    private static double Top(Window window, string name) => Named<Control>(window, name).TranslatePoint(new Point(0, 0), window)!.Value.Y; // why: the panels are in the tree

    private static int StripRows(Window window) => window.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "StripRowFrame" && b.IsEffectivelyVisible);

    [Fact]
    public Task At_1280x800_summary_needs_you_5_strip_3_and_column_tops_are_above_the_fold() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 5 && s.Strip.Rows.Count > 0, width: 1280, height: 800);

        Assert.Equal((5, 3), (shell.NeedsYou.Rows.Count, StripRows(window)));
        Assert.True(Top(window, "RunningPanel") < 800 && Top(window, "QueuedPanel") < 800);
    });

    [Fact]
    public Task At_scale_1_3_needs_you_4_and_strip_2_fit() => OnUi(async () =>
    {
        try
        {
            SettingsTheme.For(Application.Current!.Resources)(new AppearanceSettings(1.3, ReduceMotion: false)); // why: the headless session starts the app
            Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
            var (window, shell) = await Connected(s => s.NeedsYou.Count > 5 && s.Strip.Rows.Count > 0, width: 1280, height: 800);
            shell.NeedsYou.TextScale = 1.3;
            shell.Strip.TextScale = 1.3;
            Settle(window);

            Assert.Equal((4, 2), (shell.NeedsYou.Rows.Count, StripRows(window)));
            Assert.True(Top(window, "RunningPanel") < 800);
        }
        finally
        {
            SettingsTheme.For(Application.Current!.Resources)(new AppearanceSettings(1.0, ReduceMotion: false));
        }
    });
}
