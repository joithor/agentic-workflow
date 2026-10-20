namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One segment of the progress strip: done, working, or still to do.</summary>
/// <param name="Kind">"done", "working" or "todo".</param>
internal sealed record SetupSegment(string Kind);
