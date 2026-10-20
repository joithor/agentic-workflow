using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.Views.Approvals;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>The approve-all dialog's view: the hold is the person's pressing, so letting go anywhere ends it, and focus follows the dialog.</summary>
[Lane(TestLane.Integration)]
public sealed class ApproveAllDialogTests
{
    private readonly ApproveAllFixture _f = new();

    private static CancellationToken Token => ApproveAllFixture.Token;

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ApproveAllDialogTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private async Task<(Window Window, ApproveAllDialog Dialog, ApproveAllViewModel Model)> Shown()
    {
        var model = await _f.Dialog();
        var dialog = new ApproveAllDialog { DataContext = model };
        var window = new Window { Width = 900, Height = 900, Content = dialog };
        window.Show();
        dialog.FocusFirst();
        await model.Open(Token);
        Settle(window);
        return (window, dialog, model);
    }

    private static Point CenterOf(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? throw new InvalidOperationException("the control is not in the window");

    [Fact]
    public Task Letting_go_off_the_button_ends_the_hold_and_sends_nothing() => OnUi(async () =>
    {
        var (window, _, model) = await Shown();
        var hold = Named<Button>(window, "ApproveHold");
        var on = CenterOf(window, hold);

        window.MouseDown(on, MouseButton.Left);
        Assert.True(model.IsHolding);
        window.MouseMove(new Point(on.X, on.Y + 400));
        window.MouseUp(new Point(on.X, on.Y + 400), MouseButton.Left);
        Settle(window);
        _f.Clock.Advance(TimeSpan.FromSeconds(3));
        await model.Tick();

        Assert.Equal((false, 0), (model.IsHolding, _f.Server.Approvals.Count));
        window.Close();
    });

    [Fact]
    public Task The_hold_button_takes_focus_when_the_challenge_arrives_from_Close() => OnUi(async () =>
    {
        var (window, _, _) = await Shown();

        Assert.Same(Named<Button>(window, "ApproveHold"), Focused(window));
        window.Close();
    });

    [Fact]
    public Task Focus_somewhere_else_in_the_dialog_is_left_alone_when_the_challenge_arrives() => OnUi(async () =>
    {
        var model = await _f.Dialog();
        var dialog = new ApproveAllDialog { DataContext = model };
        var window = new Window { Width = 900, Height = 900, Content = dialog };
        window.Show();
        var gate = _f.Server.AppChallengeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opening = model.Open(Token);
        Settle(window);
        var row = Named<ItemsControl>(window, "ApproveRows").GetVisualDescendants().OfType<Border>().First(b => b.Focusable);
        Assert.True(row.Focus());

        gate.SetResult();
        await opening;
        Settle(window);

        Assert.Same(row, Focused(window));
        window.Close();
    });

    [Fact]
    public Task Focus_goes_to_Close_when_the_outcome_appears() => OnUi(async () =>
    {
        var (window, _, model) = await Shown();
        model.Typed = "approve-all c41e07";

        await _f.Finish(model.SubmitTyped());
        Settle(window);

        Assert.Equal((ApproveAllPhase.Done, "ApproveClose"), (model.Phase, Focused(window)?.Name));
        window.Close();
    });
}
