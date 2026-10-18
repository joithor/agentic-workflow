using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>The live preview of one session's terminal (GET /sindri/sessions/{provider}/{id}/preview; collector spec CA-9). Human callers only.</summary>
/// <param name="Ref">The session's name.</param>
/// <param name="ExpectStartedAt">The row's startedAt the caller saw, or null (none to check: the row has no startedAt, or the caller sent none); a different value is SND-SESSION-409.</param>
public sealed record GetSessionPreviewRequest(SessionRef Ref, DateTimeOffset? ExpectStartedAt = null) : IQuery<GetSessionPreviewResponse>;
