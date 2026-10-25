namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One segment of the progress strip: done, working, or still to do.</summary>
/// <param name="Kind">"done", "working" or "todo".</param>
internal sealed record SetupSegment(string Kind)
{
    /// <summary>True for a finished step.</summary>
    public bool IsDone => string.Equals(Kind, "done", StringComparison.Ordinal);

    /// <summary>True for a step Sindri is working on.</summary>
    public bool IsWorking => string.Equals(Kind, "working", StringComparison.Ordinal);
}
