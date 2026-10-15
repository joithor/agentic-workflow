namespace Aw.Meta.Core.Obligations;

/// <summary>An owner's answer to a lane's question.</summary>
/// <param name="Question">The question number.</param>
/// <param name="AskedBy">The asking lane's session name.</param>
/// <param name="Text">The answer.</param>
/// <param name="AnsweredAt">When it was recorded.</param>
public sealed record OwnerAnswer(int Question, string AskedBy, string Text, DateTimeOffset AnsweredAt);
