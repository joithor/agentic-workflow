using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Publishing;

/// <summary>An attachment already on the Linear issue.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title.</param>
public sealed record ExistingAttachment(AttachmentId Id, string Title);
