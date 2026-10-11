using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Tests.Ids;

/// <summary>Wraps a AttachmentId so the JSON tests can send a missing or null property.</summary>
public sealed record AttachmentIdHolder(AttachmentId Id);
