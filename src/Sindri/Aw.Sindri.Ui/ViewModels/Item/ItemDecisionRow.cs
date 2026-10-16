using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>A pending decision on an item's sheet: the question and one button per option as served.</summary>
/// <param name="Id">The decision's id.</param>
/// <param name="Question">The question as served.</param>
/// <param name="FiledBy">Who filed it.</param>
/// <param name="Options">The option buttons, in the served order; the recommended one is flagged.</param>
/// <param name="DecideLine">The phase and command line under the decision when Decide is not ready; empty when it is.</param>
/// <param name="DecideCli">The served command the line's Copy puts on the clipboard; empty for none.</param>
internal sealed record ItemDecisionRow(string Id, string Question, string FiledBy, IReadOnlyList<DecisionOption> Options, string DecideLine = "", string DecideCli = "")
{
    /// <summary>True when the line has a command to copy.</summary>
    internal bool CanCopy => DecideCli.Length > 0;

    /// <summary>The Copy button's word.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    internal string CopyLabel => Labels.Copy;
#pragma warning restore CA1822, MA0041

    /// <summary>What Copy is called to a screen reader: "Copy: Decide DEC-1".</summary>
    internal string CopyName => Labels.CopyDecide(Id);

    /// <summary>The line's accessible name: it carries the decision it speaks of.</summary>
    internal string LineName => Labels.DecideLineName(Id, DecideLine);
}
