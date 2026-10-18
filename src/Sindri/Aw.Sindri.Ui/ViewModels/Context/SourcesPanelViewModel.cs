namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The sources panel on the Context page (U9b owns this class); a seam until then.</summary>
internal sealed class SourcesPanelViewModel(ContextPorts ports) : ObservableObject
{
    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;
}
