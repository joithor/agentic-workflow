using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One row of the Workflows list: it passes a title click and focus arriving up to the page as routed events.</summary>
internal sealed partial class InstanceRow : UserControl
{
    /// <summary>A title click: open the instance.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<InstanceRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<InstanceRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public InstanceRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's instance address.</summary>
    internal string Address => (DataContext as InstanceRowViewModel)?.Address ?? string.Empty;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // A row with no inputs has nothing to name: its wrapper leaves the control view rather than stand as a nameless stop (review run 5, r5-1).
        if (this.FindControl<Control>("WorkflowRowInputs") is { } inputs && DataContext is InstanceRowViewModel row)
        {
            AutomationProperties.SetAccessibilityView(inputs, row.HasInputs ? AccessibilityView.Control : AccessibilityView.Raw);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty)
        {
            Compact(change.GetNewValue<PageTier>() >= PageTier.Narrow);
        }
    }

    // Behind a sheet (UI spec §3.2) the columns don't fit: the row keeps its workflow, item and stage.
    private void Compact(bool compact)
    {
        if (this.FindControl<Grid>("WorkflowRowGrid") is not { } grid)
        {
            return;
        }

        var hidden = new GridLength(0);   // the hidden columns keep their place
        grid.ColumnDefinitions = compact
            ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(hidden), new ColumnDefinition(hidden)]
            : [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(220)), new ColumnDefinition(GridLength.Star)];
        foreach (var name in (string[])["WorkflowRowAside", "WorkflowRowNow", "WorkflowRowDetails"])
        {
            this.FindControl<Control>(name)?.IsVisible = !compact;
        }

        this.FindControl<Control>("WorkflowRowCompact")?.IsVisible = compact;
    }

    private void OnCopy(object? sender, RoutedEventArgs e) => _ = (DataContext as InstanceRowViewModel)?.CopyAddress(CancellationToken.None);

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
