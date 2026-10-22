using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The hold button's wiring (setup spec §2.2, §7): the key, the pointer capture, focus and the tree each end or start a hold. The clock is fake and the test takes the timer's steps.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class HoldButtonViewTests
{
    private readonly AppConfirmFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HoldButtonViewTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private sealed record Shown(Window Window, HoldButton Button, HoldButtonViewModel Model, TextBox Other, List<int> Held);

    private Shown Show()
    {
        var model = new HoldButtonViewModel("ApproveAllRecipes", _f.Catalog(), _f.Clock);
        model.Offer(_f.Held());
        var button = new HoldButton { DataContext = model };
        var other = new TextBox();
        var held = new List<int>();
        button.Held += (_, e) => held.Add(e.HeldMs);
        var window = new Window { Width = 600, Height = 300, Content = new StackPanel { Children = { button, other } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        _ = button.Part.Focus();
        return new Shown(window, button, model, other, held);
    }

    [Theory]
    [InlineData(Key.Space, PhysicalKey.Space)]
    [InlineData(Key.Enter, PhysicalKey.Enter)]
    public Task A_key_held_to_the_served_time_sends_once(Key key, PhysicalKey physical) => OnUi(() =>
    {
        var s = Show();
        s.Window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);
        Assert.True(s.Model.IsHolding);
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1500));
        s.Button.Step();
        s.Button.Step();

        Assert.Equal([1500], s.Held);
        Assert.Equal((false, false), (s.Model.IsHolding, s.Button.Stepping));
        s.Window.Close();
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(Key.Space, PhysicalKey.Space)]
    [InlineData(Key.Enter, PhysicalKey.Enter)]
    public Task A_key_up_before_the_time_cancels_and_sends_nothing(Key key, PhysicalKey physical) => OnUi(() =>
    {
        var s = Show();
        s.Window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);
        _f.Clock.Advance(TimeSpan.FromMilliseconds(700));
        s.Window.KeyRelease(key, RawInputModifiers.None, physical, keySymbol: null);
        _f.Clock.Advance(TimeSpan.FromSeconds(3));
        s.Button.Step();

        Assert.Equal((false, false, 0), (s.Model.IsHolding, s.Button.Stepping, s.Held.Count));
        s.Window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_repeated_key_down_does_not_start_a_second_hold() => OnUi(() =>
    {
        var s = Show();
        var started = 0;
        s.Model.Started += (_, _) => started++;

        s.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, keySymbol: null);
        _f.Clock.Advance(TimeSpan.FromMilliseconds(600));
        s.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, keySymbol: null);
        s.Button.Step();

        Assert.Equal((1, 0.4d), (started, s.Model.Progress));   // a restart would have put the progress back to 0
        s.Window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_capture_lost_mid_hold_cancels_it_and_nothing_is_sent_later() => OnUi(() =>
    {
        var s = Show();
        IPointer? pointer = null;
        s.Button.Part.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var on = s.Button.Part.TranslatePoint(new Point(5, 5), s.Window) ?? throw new InvalidOperationException("not in the window");
        s.Window.MouseDown(on, MouseButton.Left);
        Assert.True(s.Model.IsHolding);

        pointer!.Capture(control: null);
        _f.Clock.Advance(TimeSpan.FromSeconds(3));
        s.Button.Step();

        Assert.Equal((false, false, 0), (s.Model.IsHolding, s.Button.Stepping, s.Held.Count));
        s.Window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Focus_leaving_mid_hold_cancels_it() => OnUi(() =>
    {
        var s = Show();
        s.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, keySymbol: null);
        Assert.True(s.Model.IsHolding);

        _ = s.Other.Focus();
        Dispatcher.UIThread.RunJobs();
        _f.Clock.Advance(TimeSpan.FromSeconds(3));
        s.Button.Step();

        Assert.Equal((false, 0), (s.Model.IsHolding, s.Held.Count));
        s.Window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Leaving_the_tree_mid_hold_stops_the_timer_and_cancels_the_hold() => OnUi(() =>
    {
        var s = Show();
        s.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, keySymbol: null);
        Assert.True(s.Button.Stepping);

        s.Window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((false, false), (s.Button.Stepping, s.Model.IsHolding));
        s.Window.Close();
        return Task.CompletedTask;
    });
}
