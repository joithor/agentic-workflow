using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One line of the Workflows strip: it passes a title click, the Answer button and focus arriving up to Home as routed events.</summary>
internal sealed partial class StripRow : UserControl
{
    /// <summary>A title click: open the instance.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<StripRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<StripRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    /// <summary>The Answer button was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> AnswerEvent = RoutedEvent.Register<StripRow, RoutedEventArgs>(nameof(AnswerEvent), RoutingStrategies.Bubble);

    public StripRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's instance address.</summary>
    internal string Address => (DataContext as StripRowViewModel)?.Address ?? string.Empty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty)
        {
            Stack(change.GetNewValue<PageTier>() >= PageTier.Narrow);
        }
    }

    // On a narrow page (beside a sheet, UI spec §3.2) the detail drops under the line so no control ends under the sheet.
    private void Stack(bool stacked)
    {
        var grid = this.FindControl<Grid>("StripGrid");
        var detail = this.FindControl<WrapPanel>("StripDetail");
        if (grid is null || detail is null)
        {
            return;
        }

        grid.RowDefinitions = stacked ? [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] : [];
        Grid.SetRow(detail, stacked ? 1 : 0);
        Grid.SetColumn(detail, stacked ? 2 : 3);
        Grid.SetColumnSpan(detail, stacked ? 4 : 1);
        detail.Margin = stacked ? new Thickness(0, 2, 0, 0) : new Thickness(8, 0);
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));

    private void OnAnswer(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(AnswerEvent, this));
}
