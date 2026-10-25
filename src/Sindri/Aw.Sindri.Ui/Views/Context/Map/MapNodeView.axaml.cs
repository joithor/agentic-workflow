using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>One node of the context map: a chip or a card, bound to a <see cref="ViewModels.Context.MapNodeViewModel"/>.</summary>
internal sealed partial class MapNodeView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="MapNodeView"/> class.</summary>
    public MapNodeView()
    {
        InitializeComponent();
    }
}
