using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>What gh-publish did: pushed, or blocked on review with nothing pushed.</summary>
/// <param name="Provenance">The run's data provenance (only seeded data is pushed).</param>
/// <param name="ReviewBlocked">Whether some image is not in the reviewed list: nothing was pushed.</param>
/// <param name="Unreviewed">The images still to open and review.</param>
/// <param name="Branch">The evidence branch, ui-evidence/pr-&lt;n&gt;; null when nothing was pushed.</param>
/// <param name="Commit">The commit that holds the images; null when nothing was pushed.</param>
/// <param name="Urls">Local image path to its commit-pinned image URL; empty when nothing was pushed.</param>
/// <param name="CommentFile">The pr-comment.md written in the run directory; null when nothing was pushed.</param>
/// <param name="Post">The argv that posts the comment (gh pr comment ... --body-file ...); empty when nothing was pushed.</param>
public sealed record GhPublishResponse(
    Provenance Provenance,
    bool ReviewBlocked,
    IReadOnlyList<string> Unreviewed,
    string? Branch,
    string? Commit,
    IReadOnlyDictionary<string, string> Urls,
    string? CommentFile,
    IReadOnlyList<string> Post);
