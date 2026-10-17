using System.ComponentModel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// What shows one fact when its row is opened (screens §5.2). A viewer is a view model with no Avalonia type; the view that draws it
/// is registered beside it (FactViewTemplates), so a new fact type adds a viewer and a view and edits no file of this one.
/// </summary>
internal interface IFactViewer : INotifyPropertyChanged
{
    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>Reads what the viewer shows beyond the row (the fact's body); a viewer that needs nothing completes at once.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the viewer shows its answer.</returns>
    public Task Load(CancellationToken cancellationToken);
}
