namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run drawer's take over (<see cref="RunInspectorViewModel"/>, continued).</summary>
internal sealed partial class RunInspectorViewModel
{
    /// <summary>What the last Take over came to, in words; empty before one.</summary>
    public string TakeOverLine { get; } = string.Empty;
}
