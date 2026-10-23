using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.Views.Components;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The hold-to-approve button (setup spec §2.2): a pointer press, or Space or Enter held, starts the hold and a 50 ms timer steps it; a pointer
/// release anywhere, a lost capture, losing focus or a key-up before the time lets go; a repeated key-down is ignored. The fill is a clipped
/// border that grows with the hold (hidden under reduced motion, where the words count instead). It reports a complete hold through
/// <see cref="Held"/> and sends nothing itself. When the catalog row is not ready the lock button's terminal copy shows in its place.
/// </summary>
internal sealed class HoldButton : UserControl
{
    private static readonly TimeSpan _tickEvery = TimeSpan.FromMilliseconds(50);

    private readonly Border _fill = new() { HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
    private readonly TextBlock _words = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ProtectedActionButton _terminal = new() { IsVisible = false };
    private readonly DispatcherTimer _timer;
    private IPointer? _pointer;   // the pointer holding the button, captured so a release anywhere reaches it
    private HoldButtonViewModel? _model;

    public HoldButton()
    {
        _timer = new DispatcherTimer(_tickEvery, DispatcherPriority.Normal, (_, _) => Step()) { IsEnabled = false };
        _fill.Classes.Add("hold-fill");
        _words.Classes.Add("button");
        AutomationProperties.SetAccessibilityView(_words, AccessibilityView.Raw);
        Part.Classes.Add("hold-part");
        Part.Content = new Border { ClipToBounds = true, CornerRadius = new CornerRadius(5), Child = new Grid { Children = { _fill, _words } } };
        Part.SizeChanged += (_, _) => Draw();
        Part.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        Part.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
        Part.AddHandler(PointerCaptureLostEvent, OnLetGo, RoutingStrategies.Direct);   // the event is Direct-only: a Tunnel subscription is never called
        Part.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Part.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        Part.LostFocus += (_, _) => Release();
        Content = new StackPanel { Spacing = 8, Children = { Part, _terminal } };
    }

    /// <summary>Raised once when a hold reaches the server's time; the argument carries the time held. The host sends the answer.</summary>
    internal event EventHandler<HeldEventArgs>? Held;

    /// <summary>The name of the inner button (the part that takes focus and the pointer), so a host's tests and focus code can find it.</summary>
    public string PartName
    {
        get => Part.Name ?? string.Empty;
        set => Part.Name = value;
    }

    /// <summary>The inner button.</summary>
    internal Button Part { get; } = new() { Name = "HoldPart" };

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Watch(DataContext as HoldButtonViewModel);
        Draw();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(_model);
        Draw();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Release();   // a hold in progress ends with the control: no timer, no capture, nothing sent
        _model?.PropertyChanged -= OnModelChanged;
        base.OnDetachedFromVisualTree(e);
    }

    // One subscription at most, whichever way the control comes to be watching.
    private void Watch(HoldButtonViewModel? model)
    {
        _model?.PropertyChanged -= OnModelChanged;
        _model = model;
        _model?.PropertyChanged += OnModelChanged;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => Draw();

    private void Draw()
    {
        _terminal.DataContext = _model?.Terminal;
        _terminal.IsVisible = _model is { IsReady: false };
        Part.IsVisible = _model is null or { IsReady: true };
        if (_model is not { } model)
        {
            return;
        }

        _words.Text = model.Text;
        AutomationProperties.SetName(Part, model.Name);
        AutomationProperties.SetHelpText(Part, model.Help);
        _fill.IsVisible = model.ShowsFill;
        _fill.Width = Part.Bounds.Width * model.Progress;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_model is not { } model || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = model.Begin();
        _timer.IsEnabled = model.IsHolding;
        if (model.IsHolding)
        {
            e.Pointer.Capture(Part);   // a release anywhere, or a lost capture, reaches the button and ends the hold
            _pointer = e.Pointer;
        }

        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        Release();
        e.Handled = true;
    }

    private void OnLetGo(object? sender, PointerCaptureLostEventArgs e) => Release();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter && _model is { } model)
        {
            if (!model.IsHolding)
            {
                _ = model.Begin();   // an auto-repeated key-down finds it held already and does nothing
                _timer.IsEnabled = model.IsHolding;
            }

            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            Release();
            e.Handled = true;
        }
    }

    // Let go before the end: the fill resets and nothing is sent.
    private void Release()
    {
        _timer.IsEnabled = false;
        ReleaseCapture();
        _model?.Cancel();
    }

    private void ReleaseCapture()
    {
        var pointer = _pointer;
        _pointer = null;
        pointer?.Capture(control: null);
    }

    /// <summary>True while the 50 ms timer is stepping the hold.</summary>
    internal bool Stepping => _timer.IsEnabled;

    /// <summary>One step of the hold, as the timer takes it (a test takes it by hand on a fake clock).</summary>
    internal void Step()
    {
        if (_model is not { } model)
        {
            return;
        }

        var held = model.Tick();
        if (!model.IsHolding)
        {
            _timer.IsEnabled = false;
            ReleaseCapture();
        }

        if (held is { } ms)
        {
            Held?.Invoke(this, new HeldEventArgs(ms));
        }
    }
}
