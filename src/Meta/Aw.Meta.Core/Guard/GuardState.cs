namespace Aw.Meta.Core.Guard;

/// <summary>What the stop-check loop guard remembers between calls.</summary>
/// <param name="Fingerprint">The open set the last block was for; empty before any.</param>
/// <param name="Count">How many times in a row that set blocked.</param>
public sealed record GuardState(string Fingerprint, int Count)
{
    /// <summary>The state before any block.</summary>
    public static GuardState None { get; } = new(string.Empty, 0);
}
