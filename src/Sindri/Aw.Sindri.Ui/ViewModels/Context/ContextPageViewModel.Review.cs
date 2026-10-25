using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The review seam (U9c owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The map review queue.</summary>
    public MapReviewViewModel Review { get; private set; }

    [MemberNotNull(nameof(Review))]
    private void InitReview() => Review = new MapReviewViewModel(Ports);
}
