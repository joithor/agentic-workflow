namespace Aw.Meta.Features.OwnerAnswer;

/// <summary>The AnswerOwner request: record the owner's answer to a queued question.</summary>
/// <param name="Question">The question number.</param>
/// <param name="Answer">The owner's answer.</param>
public sealed record AnswerOwnerRequest(int Question, string Answer);
