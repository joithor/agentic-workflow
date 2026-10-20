using Progress = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The legend line at the foot of the Workflows page (screens §4.2): each mark the rows draw with its word, then the page's keys. The marks come from where the rows get them.</summary>
internal static class WorkflowsLegend
{
    /// <summary>The line as drawn.</summary>
    internal static readonly string Text =
        $"{StageCellViewModel.Mark(Progress.Done)} {Labels.StageDone}   {StageCellViewModel.Mark(Progress.Active)} {Labels.StageActive}   "
        + $"{StageCellViewModel.Mark(Progress.Pending)} {Labels.StagePending}   {Labels.MarkEscalated} {Labels.LegendEscalated}   {Labels.MarkTakenOver} {Labels.LegendTakenOver}   {Labels.LoopMark} {Labels.LegendLoop}"
        + "        Enter opens · A answers a decision or finishes a task that is waiting for you · B shows how to hand a run back · X cancels";

    /// <summary>The line in words, for screen readers: no glyph.</summary>
    internal const string Name =
        $"{Labels.StageDone}, {Labels.StageActive}, {Labels.StagePending}, {Labels.LegendEscalated}, {Labels.LegendTakenOver}, {Labels.LegendLoop}. Keys: Enter opens, A answers a decision or finishes a task that is waiting for you, B shows how to hand a run back, X cancels";
}
