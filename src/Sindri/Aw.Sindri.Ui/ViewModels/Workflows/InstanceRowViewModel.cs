using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One row of the Workflows list (screens §4.2): the subject, the inputs, a cell per state, and the served now and chip.</summary>
internal sealed class InstanceRowViewModel : ObservableObject
{
    internal InstanceRowViewModel(InstanceSummary row)
    {
        _ = row;
    }

    /// <summary>The instance's address, as served.</summary>
    public string Address { get; } = string.Empty;

    /// <summary>The subject: its served label.</summary>
    public string Subject { get; } = string.Empty;

    /// <summary>The served chip.</summary>
    public StateChip Chip { get; } = new(string.Empty, ChipTone.Neutral);

    /// <summary>The served now sentence, unchanged.</summary>
    public string Now { get; } = string.Empty;

    /// <summary>A cell per declared input.</summary>
    public IReadOnlyList<InputCellViewModel> Inputs { get; } = [];

    /// <summary>A cell per state, in the served order.</summary>
    public IReadOnlyList<StageCellViewModel> Stages { get; } = [];

    /// <summary>The name screen readers hear: the subject and the address.</summary>
    public string AccessibleName { get; } = string.Empty;

    /// <summary>Where Enter and a click go.</summary>
    public DeepLink Link { get; } = new(DeepLinkKind.Workflow, string.Empty);

    /// <summary>True while the views are stalled or rebuilding.</summary>
    public bool IsStale { get; }
}
