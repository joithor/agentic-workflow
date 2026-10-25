namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The checklist should come into view, at one step when a person pressed Continue.</summary>
internal sealed class SetupRevealEventArgs : EventArgs
{
    internal SetupRevealEventArgs(string? stepId)
    {
        StepId = stepId;
    }

    /// <summary>The step to bring into view, or null for the top of the checklist.</summary>
    internal string? StepId { get; }
}
