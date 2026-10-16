using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The instance frame inside the sheet: Back closes the sheet like Esc, and Open in hands the subject's link to the platform.</summary>
internal sealed partial class InstanceFrameView : UserControl
{
    private const int MaxLooks = 20;

    private InstanceFrameViewModel? _frame;
    private FrameFocusEventArgs? _wanted;
    private int _attempts;

    public InstanceFrameView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _frame?.FocusRequested -= OnFocusRequested;
        base.OnDataContextChanged(e);
        _frame = DataContext as InstanceFrameViewModel;
        _frame?.FocusRequested += OnFocusRequested;
        if (_frame?.WantedFocus is { } waiting)
        {
            OnFocusRequested(_frame, waiting);   // asked for before this view had its context
        }
    }

    // A key opened this frame wanting focus on one of its parts (A on a desk task: the awaiting output's Copy; X: Keep). The part may not be
    // laid out yet, and the sheet's own focus move may follow, so it is tried after each layout pass and once more after the queue
    // has run, until it holds focus.
    private void OnFocusRequested(object? sender, FrameFocusEventArgs e)
    {
        _wanted = e;
        _attempts = 0;
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        Dispatcher.UIThread.Post(Satisfy, DispatcherPriority.Background);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Satisfy();

    private void Satisfy()
    {
        if (_wanted is not { } wanted)
        {
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }

        var target = Part(wanted);
        if (target?.IsFocused == true && _attempts++ > 0)
        {
            (_wanted, _attempts) = (null, 0);   // held it through a second look: the sheet's own move did not take it back
            _frame?.FocusServed();
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }

        if (target is not null)
        {
            _ = target.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.Post(Satisfy, DispatcherPriority.Background);
        }
        else if (++_attempts > MaxLooks)
        {
            (_wanted, _attempts) = (null, 0);
            _frame?.FocusServed();
            LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private Button? Part(FrameFocusEventArgs wanted)
    {
        var buttons = this.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        var name = wanted.Target switch
        {
            FrameFocus.Keep => "ExpandedKeep",
            FrameFocus.OpenIn => "InstanceOpenIn",
            FrameFocus.CopyStatus or FrameFocus.PendingOutput or FrameFocus.Back => "ExpandedCopyStatus",
            _ => throw new ArgumentOutOfRangeException(nameof(wanted), wanted.Target, message: null),
        };
        return wanted.Target == FrameFocus.PendingOutput
            ? buttons.Find(b => string.Equals(b.Name, "FactCopyCli", StringComparison.Ordinal) && b.DataContext is FactRowViewModel row && string.Equals(row.Key, wanted.Key, StringComparison.Ordinal))
                ?? buttons.Find(b => string.Equals(b.Name, "ExpandedCopyStatus", StringComparison.Ordinal))
            : buttons.Find(b => string.Equals(b.Name, name, StringComparison.Ordinal));
    }

    private void OnBack(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel)?.Back();

    private void OnOpenIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceFrameViewModel { Subject: { HasHref: true } subject } && Uri.TryCreate(subject.Href, UriKind.Absolute, out var uri))
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        }
    }
}
