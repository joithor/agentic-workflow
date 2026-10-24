namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The links of one band in the node panel.</summary>
/// <param name="Title">The band's name.</param>
/// <param name="Rows">Its links.</param>
internal sealed record NodeEdgeGroupViewModel(string Title, IReadOnlyList<NodeEdgeRowViewModel> Rows);
