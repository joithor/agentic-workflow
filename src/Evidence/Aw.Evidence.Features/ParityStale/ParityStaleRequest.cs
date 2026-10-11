using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Features.ParityStale;

/// <summary>Picks the pixel-diff attachments to delete after a re-upload.</summary>
/// <param name="AttachmentsFile">A JSON array of the issue's attachments, each {id, title} (absolute; other fields are ignored).</param>
/// <param name="Keep">The attachments just uploaded, which stay.</param>
public sealed record ParityStaleRequest(string AttachmentsFile, IReadOnlyList<AttachmentId> Keep);
