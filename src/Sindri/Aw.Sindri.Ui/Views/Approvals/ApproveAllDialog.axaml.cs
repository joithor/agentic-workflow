using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Views.Approvals;

/// <summary>
/// The approve-all dialog: the hold button fills while it is pressed (pointer, or Space or Enter held; a repeated key-down is ignored and only the
/// key-up lets go) and answers by itself at the server's minimum; Esc and Close shut it with nothing sent.
/// </summary>
internal sealed partial class ApproveAllDialog : UserControl
{
    private static readonly TimeSpan _tickEvery = TimeSpan.FromMilliseconds(50);

    private readonly DispatcherTimer _fill;
    private IPointer? _pointer;   // the pointer holding the button, captured so a release anywhere reaches it
    private INotifyPropertyChanged? _watched;

    public ApproveAllDialog()
    {
        InitializeComponent();
        _fill = new DispatcherTimer(_tickEvery, DispatcherPriority.Normal, OnFill) { IsEnabled = false };
        var hold = this.Get<Button>("ApproveHold");
        hold.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        hold.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
        hold.AddHandler(PointerCaptureLostEvent, OnLetGo, RoutingStrategies.Tunnel);
        hold.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        hold.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        hold.LostFocus += (_, _) => Release();
    }

    /// <summary>Moves focus to the hold button, or to Close when there is nothing to approve (the dialog just opened).</summary>
    internal void FocusFirst()
    {
        var target = DataContext is ApproveAllViewModel { CanApprove: true, OffersHold: true } ? this.Get<Button>("ApproveHold") : this.Get<Button>("ApproveClose");
        _ = target.Focus(NavigationMethod.Tab);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnModelChanged;
        }

        _watched = DataContext as INotifyPropertyChanged;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnModelChanged;
        }
    }

    // Focus follows what the dialog is waiting for: the hold button when the challenge arrives, Close (or Done) when the approval runs or ends.
    // It moves only from nowhere, from Close, or from a control that just left the screen, never away from where the person is typing or reading.
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ApproveAllViewModel dialog)
        {
            return;
        }

        if (e.PropertyName is nameof(ApproveAllViewModel.CanApprove) or nameof(ApproveAllViewModel.OffersHold) && dialog is { CanApprove: true, OffersHold: true })
        {
            Dispatcher.UIThread.Post(() => MoveFocusTo("ApproveHold"), DispatcherPriority.Loaded);
        }
        else if (e.PropertyName == nameof(ApproveAllViewModel.Phase) && dialog.Phase is ApproveAllPhase.Applying or ApproveAllPhase.Done or ApproveAllPhase.Failed)
        {
            Dispatcher.UIThread.Post(() => MoveFocusTo("ApproveClose"), DispatcherPriority.Loaded);
        }
    }

    private void MoveFocusTo(string name)
    {
        var target = this.Get<Button>(name);
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (target.IsEffectivelyVisible && (focused is null || !focused.IsEffectivelyVisible || ReferenceEquals(focused, this.Get<Button>("ApproveClose"))))
        {
            _ = target.Focus(NavigationMethod.Tab);
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ApproveAllViewModel dialog && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            dialog.BeginHold();
            _fill.IsEnabled = dialog.IsHolding;
            if (dialog.IsHolding && sender is IInputElement hold)
            {
                e.Pointer.Capture(hold);   // a release anywhere, or a lost capture, reaches the button and ends the hold
                _pointer = e.Pointer;
            }

            e.Handled = true;
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        Release();
        e.Handled = true;
    }

    private void OnLetGo(object? sender, PointerCaptureLostEventArgs e) => Release();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter && DataContext is ApproveAllViewModel dialog)
        {
            if (!dialog.IsHolding)
            {
                dialog.BeginHold();   // an auto-repeated key-down finds it held already and does nothing
                _fill.IsEnabled = dialog.IsHolding;
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
        _fill.IsEnabled = false;
        ReleaseCapture();
        (DataContext as ApproveAllViewModel)?.CancelHold();
    }

    private void ReleaseCapture()
    {
        var pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);
    }

    private void OnFill(object? sender, EventArgs e)
    {
        if (DataContext is ApproveAllViewModel dialog)
        {
            _ = Step(dialog);
        }
    }

    // One step of the fill; the timer stops when the hold is over (let go, or the answer went).
    private async Task Step(ApproveAllViewModel dialog)
    {
        await dialog.Tick().ConfigureAwait(true);
        _fill.IsEnabled = dialog.IsHolding;
        if (!dialog.IsHolding)
        {
            ReleaseCapture();
        }
    }

    private void OnTyped(object? sender, RoutedEventArgs e) => _ = (DataContext as ApproveAllViewModel)?.SubmitTyped();

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = (DataContext as ApproveAllViewModel)?.Retry();

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as ApproveAllViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as ApproveAllViewModel)?.Close();

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ApproveAllViewModel dialog && sender is Control { DataContext: ApproveAllLine { Cli: var cli } })
        {
            _ = dialog.Copy(cli);
        }
    }
}
