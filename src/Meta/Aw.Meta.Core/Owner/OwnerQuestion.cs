namespace Aw.Meta.Core.Owner;

/// <summary>A question for the owner and its answer.</summary>
/// <param name="Number">The question number.</param>
/// <param name="AskedBy">The asking session's name.</param>
/// <param name="Text">The question.</param>
/// <param name="AskedAt">When it was queued.</param>
public sealed record OwnerQuestion(int Number, string AskedBy, string Text, DateTimeOffset AskedAt)
{
    /// <summary>The owner's answer; null while open.</summary>
    public string? Answer { get; init; }

    /// <summary>When it was answered.</summary>
    public DateTimeOffset? AnsweredAt { get; init; }
}
