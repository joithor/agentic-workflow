using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's tab strip (screens §4.2): Timeline, Decisions, Requests and Context with the counts the instance serves. Their content arrives with later phases; for now each is its count and the phase that brings it.</summary>
internal sealed class FrameTabsViewModel
{
    internal FrameTabsViewModel(ViewCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        Tabs =
        [
            new FrameTabViewModel(Labels.TabTimeline, counts.Timeline, Labels.TabsArriveU8b),
            new FrameTabViewModel(Labels.TabDecisions, counts.Decisions, Labels.TabsArriveU8b),
            new FrameTabViewModel(Labels.TabRequests, counts.Requests, Labels.TabsArriveU8b),
            new FrameTabViewModel(Labels.TabContext, counts.Context, Labels.TabArrivesU9),
        ];
    }

    /// <summary>The tabs, in order.</summary>
    public IReadOnlyList<FrameTabViewModel> Tabs { get; }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The strip's accessible name.</summary>
    public string Name => Labels.TabsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>What the strip draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', Tabs.Select(t => t.Name));
}
