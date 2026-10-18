namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The side column seams: the node card and the sources panel (U9b owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The node card.</summary>
    public NodeCardViewModel Node { get; } = new();

    /// <summary>The sources panel.</summary>
    public SourcesPanelViewModel Sources { get; } = new();
}
