namespace Aw.Evidence.Features.ParityComment;

/// <summary>Where a parity comment is posted.</summary>
public enum CommentTarget
{
    /// <summary>The Linear issue (the images go there as attachments).</summary>
    Linear,

    /// <summary>The pull request (a table only; the images stay on Linear).</summary>
    GitHub,
}
