using Aw.Evidence.Core.Publishing;

namespace Aw.Evidence.Features.ParityStale;

/// <summary>The stale attachments.</summary>
/// <param name="Stale">Every 'Pixel diff: ' attachment not kept.</param>
public sealed record ParityStaleResponse(IReadOnlyList<ExistingAttachment> Stale);
