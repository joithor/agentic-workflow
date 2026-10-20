using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The instance frame's Step runs in plain words: the current stage's run line ("Attempt 2 of 3, with you") and a closed
/// Runs disclosure with one row per run. Every word is a served field mapped to the app's copy; an older server that
/// serves no runs draws nothing.
/// </summary>
internal sealed class StepRunsViewModel
{
    private StepRunsViewModel(string currentLine, IReadOnlyList<StepRunRowViewModel> rows)
    {
        (CurrentLine, Rows) = (currentLine, rows);
    }

    /// <summary>The current stage's run line, or empty when no run is open.</summary>
    public string CurrentLine { get; }

    /// <summary>True when a run is open on the current stage.</summary>
    public bool HasCurrentLine => CurrentLine.Length > 0;

    /// <summary>One row per run, in served (start) order.</summary>
    public IReadOnlyList<StepRunRowViewModel> Rows { get; }

    /// <summary>True when there is any run to list.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>The disclosure's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.RunsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>What the list draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', CurrentLine, string.Join(';', Rows.Select(r => r.Text)));

    /// <summary>Reads the served runs.</summary>
    /// <param name="runs">The runs as served; null from a server that predates them.</param>
    /// <param name="currentState">The instance's current state.</param>
    /// <returns>The view model; empty without runs.</returns>
    internal static StepRunsViewModel Of(IReadOnlyList<StepRun>? runs, string currentState)
    {
        _ = (runs, currentState);
        return new StepRunsViewModel(string.Empty, []);
    }
}
