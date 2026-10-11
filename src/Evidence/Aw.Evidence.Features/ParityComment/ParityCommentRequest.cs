namespace Aw.Evidence.Features.ParityComment;

/// <summary>Renders a parity run's comment for Linear or GitHub.</summary>
/// <param name="RunDirectory">The run directory holding parity.json (absolute).</param>
/// <param name="Target">Where the comment goes.</param>
public sealed record ParityCommentRequest(string RunDirectory, CommentTarget Target);
