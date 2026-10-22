namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run drawer asked for the previous or the next run.</summary>
/// <param name="step">-1 for the previous run, 1 for the next.</param>
internal sealed class StepRequestedEventArgs(int step) : EventArgs
{
    /// <summary>-1 for the previous run, 1 for the next.</summary>
    public int Step { get; } = step;
}
