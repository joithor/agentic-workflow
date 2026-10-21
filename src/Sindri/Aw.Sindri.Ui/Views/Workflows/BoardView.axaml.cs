using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The Workflows board: the lanes of <see cref="ViewModels.Workflows.WorkflowsViewModel.Lanes"/>, as tall as the viewport and at least 420.</summary>
internal sealed partial class BoardView : UserControl
{
    private const double MinLaneHeight = 420;
    private const double Margins = 28;

    public BoardView()
    {
        InitializeComponent();
        if (this.FindControl<ScrollViewer>("WorkflowsBoardScroll") is { } scroll && this.FindControl<ItemsControl>("WorkflowsBoardLanes") is { } lanes)
        {
            scroll.SizeChanged += (_, e) => lanes.Height = Math.Max(MinLaneHeight, e.NewSize.Height - Margins);
        }
    }
}
