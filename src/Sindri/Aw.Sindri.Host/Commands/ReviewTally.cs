namespace Aw.Sindri.Host.Commands;

/// <summary>A sent request's weight in the summary; counted only after the server answers.</summary>
/// <param name="Outcome">The counter.</param>
/// <param name="Count">How many items it covers.</param>
/// <param name="Label">The item's new name after a rename, or null.</param>
/// <param name="Parent">The item's new parent after a move, or null.</param>
internal sealed record ReviewTally(ReviewOutcome Outcome, int Count = 1, string? Label = null, string? Parent = null);
