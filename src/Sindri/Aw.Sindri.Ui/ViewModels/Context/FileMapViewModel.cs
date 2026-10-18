namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map on the Context page (U9d owns this class); a seam until then.</summary>
internal sealed class FileMapViewModel(ContextPorts ports) : ObservableObject
{
    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;
}
