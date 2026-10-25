namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What happened to review comments.</summary>
/// <param name="Total">All comments seen.</param>
/// <param name="Held">Comments held for you.</param>
/// <param name="WithoutOwner">Comments handled without you.</param>
/// <param name="ByType">One row per comment type, as served.</param>
internal sealed record BabysitterCommentsView(int Total, int Held, int WithoutOwner, IReadOnlyList<BabysitterCommentTypeView> ByType);
