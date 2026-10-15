using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="ITray"/> passes: the fake, and the head's adapter (on the UI thread of a headless app).</summary>
public abstract class TrayContract
{
    private static readonly TrayView _neutral = new(TrayState.Neutral, "Sindri · running", "Open Sindri", "Quit");

    /// <summary>A fresh tray.</summary>
    /// <returns>The tray.</returns>
    protected abstract ITray Create();

    [Fact]
    public void It_shows_nothing_before_the_first_view() => Expect.True(Create().Shown is null);

    [Fact]
    public void It_shows_the_last_view_given()
    {
        var tray = Create();
        var notRunning = new TrayView(TrayState.NotRunning, "Sindri isn't running · run sindri", "Open Sindri", "Quit");

        tray.Show(_neutral);
        tray.Show(notRunning);

        Expect.Equal(notRunning, tray.Shown);
    }

    [Fact]
    public void Every_state_shows()
    {
        var tray = Create();
        foreach (var state in Enum.GetValues<TrayState>())
        {
            var view = _neutral with { State = state };
            tray.Show(view);
            Expect.Equal(view, tray.Shown);
        }
    }

    // Menu labels are the app's words; one with an underscore (an access-key marker on some platforms) shows as written.
    [Fact]
    public void Labels_with_underscores_show_as_given()
    {
        var tray = Create();
        var view = _neutral with { Tooltip = "Sindri · repo_name", OpenLabel = "Open_Sindri" };

        tray.Show(view);

        Expect.Equal(view, tray.Shown);
    }
}
