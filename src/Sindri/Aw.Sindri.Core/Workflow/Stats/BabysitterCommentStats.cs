namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Review comments the babysitters handled, from the merge records.</summary>
/// <param name="Total">All comment rows.</param>
/// <param name="Held">Rows whose outcome is held for the owner.</param>
/// <param name="WithoutOwner">Rows handled without the owner (every outcome but held).</param>
/// <param name="ByType">The rows by judge type and outcome, by type name.</param>
public sealed record BabysitterCommentStats(int Total, int Held, int WithoutOwner, IReadOnlyList<CommentTypeStats> ByType);
