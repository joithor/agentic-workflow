using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.Views.Components;

#pragma warning disable MA0182 // until the U8 pages place the component
/// <summary>
/// The lock button (screens fixture "The protected-action label"): one template for every protected action. A click, Enter
/// or Space opens its popover; Esc closes it and focus returns to the button. The view draws and routes; it sends nothing.
/// </summary>
internal sealed partial class ProtectedActionButton : UserControl
{
    private ProtectedActionViewModel? _model;

    public ProtectedActionButton()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Popover.Opened += (_, _) => _model?.SetOpen(open: true);
        Popover.Closed += OnClosed;
    }

    private Flyout Popover => (Flyout)LockButton.Flyout!; // why: the button's flyout is the Flyout the XAML declares

    /// <summary>True when the button has its popover.</summary>
    internal bool HasPopover => LockButton.Flyout is not null;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _model = DataContext as ProtectedActionViewModel;
        // why: the Flyout's content is the panel the XAML declares; it is outside the logical tree until it shows, so it takes the context here
        ((Control)Popover.Content!).DataContext = _model;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _model?.SetOpen(open: false);
        Dispatcher.UIThread.Post(() => LockButton.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (_model is { } model)
        {
            _ = model.Copy(CancellationToken.None);
        }
    }
}
#pragma warning restore MA0182
