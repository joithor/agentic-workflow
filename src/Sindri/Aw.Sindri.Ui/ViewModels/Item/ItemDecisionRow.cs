using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>A pending decision on an item's sheet: the question and one button per option as served.</summary>
/// <param name="Id">The decision's id.</param>
/// <param name="Question">The question as served.</param>
/// <param name="FiledBy">Who filed it.</param>
/// <param name="Options">The option buttons, in the served order; the recommended one is flagged.</param>
internal sealed record ItemDecisionRow(string Id, string Question, string FiledBy, IReadOnlyList<DecisionOption> Options);
