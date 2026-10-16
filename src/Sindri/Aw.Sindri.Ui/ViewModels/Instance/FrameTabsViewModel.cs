#pragma warning disable CA1822, MA0041, IDE0060 // red stub: replaced by the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's tab strip (screens §4.2): Timeline, Decisions, Requests and Context with the counts the instance serves. The tabs' content arrives later; for now they are counts.</summary>
internal sealed class FrameTabsViewModel
{
    internal FrameTabsViewModel(ViewCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
    }

    /// <summary>The tabs, in order.</summary>
    public IReadOnlyList<FrameTabViewModel> Tabs { get; } = [];

    /// <summary>The strip's accessible name.</summary>
    public string Name => string.Empty;

    /// <summary>What the strip draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Empty;
}
