using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The side column seams: the node card and the sources panel (U9b owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The node card.</summary>
    public NodeCardViewModel Node { get; private set; }

    /// <summary>The sources panel.</summary>
    public SourcesPanelViewModel Sources { get; private set; }

    [MemberNotNull(nameof(Node), nameof(Sources))]
    private void InitSide()
    {
        Node = new NodeCardViewModel(Ports);
        Sources = new SourcesPanelViewModel(Ports);
    }
}
