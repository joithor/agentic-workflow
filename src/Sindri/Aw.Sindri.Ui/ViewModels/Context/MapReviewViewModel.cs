namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The map review queue on the Context page (U9c owns this class); a seam until then.</summary>
internal sealed class MapReviewViewModel(ContextPorts ports) : ObservableObject
{
    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;
}
