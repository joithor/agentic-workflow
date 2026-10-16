namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>A request filed against an item, as its sheet lists it.</summary>
/// <param name="Id">The request's id.</param>
/// <param name="Kind">The kind as served (rescope, add-task, note, handback).</param>
/// <param name="Text">What was asked.</param>
/// <param name="Actor">Who filed it.</param>
/// <param name="State">Its state as served (open, accepted, declined, voided).</param>
/// <param name="DecisionId">The decision it raised.</param>
internal sealed record ItemRequestRow(string Id, string Kind, string Text, string Actor, string State, string DecisionId);
