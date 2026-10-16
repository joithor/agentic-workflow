using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.Views.Components;

#pragma warning disable MA0182 // until the U8 pages place the component
/// <summary>
/// The Attach component (screens spec §4.5): one template in two sizes (<see cref="IsCompact"/> for row actions, normal
/// for frame headers). The chevron is a button with a menu, so it is reachable and opens from the keyboard; Esc closes
/// the menu and focus returns to the chevron. Choosing Cancel moves focus to Keep (the safe answer); either answer
/// returns it to the chevron. The view draws and routes; the view model decides.
/// </summary>
internal sealed partial class AttachButton : UserControl
{
    /// <summary>True for the compact size.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<AttachButton, bool>(nameof(IsCompact));

    private AttachControlViewModel? _model;

    private MenuFlyout Menu => (MenuFlyout)AttachChevron.Flyout!; // why: the chevron's flyout is the MenuFlyout the XAML declares

    public AttachButton()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Menu.Opened += (_, _) => _model?.MenuOpened();
        Menu.Closed += OnMenuClosed;
    }

    /// <summary>True for the compact size (row actions); false for the normal size (frame headers).</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            Classes.Set("compact", IsCompact);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _model?.PropertyChanged -= OnModelChanged;

        _model = DataContext as AttachControlViewModel;
        _model?.PropertyChanged += OnModelChanged;

        FillMenu();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(AttachControlViewModel.Menu), StringComparison.Ordinal))
        {
            FillMenu();
        }
        else if (string.Equals(e.PropertyName, nameof(AttachControlViewModel.IsConfirmingCancel), StringComparison.Ordinal))
        {
            var target = _model?.IsConfirmingCancel == true ? (Control)KeepButton : AttachChevron;
            Dispatcher.UIThread.Post(() => target.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void FillMenu()
    {
        Menu.Items.Clear();
        foreach (var entry in _model?.Menu ?? [])
        {
            var item = new MenuItem { Header = entry.Label };
            Avalonia.Automation.AutomationProperties.SetName(item, entry.Name);
            item.Click += (_, _) => _model?.Choose(entry.Action);
            _ = Menu.Items.Add(item);
        }
    }

    // Esc (or a choice) closes the menu: focus goes back to the chevron, unless the choice was Cancel, which puts it on Keep.
    private void OnMenuClosed(object? sender, EventArgs e)
    {
        if (_model?.IsConfirmingCancel != true)
        {
            Dispatcher.UIThread.Post(() => AttachChevron.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void OnPrimary(object? sender, RoutedEventArgs e) => _model?.Choose(_model.PrimaryAction);

    private void OnConfirm(object? sender, RoutedEventArgs e) => _model?.ConfirmCancel();

    private void OnKeep(object? sender, RoutedEventArgs e) => _model?.Keep();
}
#pragma warning restore MA0182
