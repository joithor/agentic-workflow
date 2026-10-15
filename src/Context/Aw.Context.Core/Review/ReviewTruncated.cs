namespace Aw.Context.Core.Review;

/// <summary>Which groups of the queue lost entries to the call's limit.</summary>
/// <param name="Deterministic">Deterministic proposals.</param>
/// <param name="Model">Model proposals and suggested links.</param>
/// <param name="Orphaned">Orphaned overrides.</param>
public sealed record ReviewTruncated(bool Deterministic, bool Model, bool Orphaned)
{
    /// <summary>Nothing cut.</summary>
    public static ReviewTruncated None { get; } = new(Deterministic: false, Model: false, Orphaned: false);
}
