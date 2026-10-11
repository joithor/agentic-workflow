using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Finds earlier evidence to offer for deletion once the new attachments exist. Never deletes anything.</summary>
public static class StaleAttachments
{
    /// <summary>This tool's earlier attachments (title prefix) that are not among the ones to keep.</summary>
    /// <param name="existing">The issue's attachments.</param>
    /// <param name="keep">The new attachments' ids.</param>
    /// <returns>The stale attachments, in order.</returns>
    public static IReadOnlyList<ExistingAttachment> Select(IReadOnlyList<ExistingAttachment> existing, IReadOnlyCollection<AttachmentId> keep) =>
        [.. existing.Where(a => a.Title.StartsWith(ParityAttachments.TitlePrefix, StringComparison.Ordinal)
                                && !keep.Any(k => string.Equals(k.Value, a.Id.Value, StringComparison.Ordinal)))];
}
