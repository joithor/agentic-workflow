namespace Aw.Context.Core.Review;

/// <summary>A review file checked before it is applied: what a protected apply binds its typed confirmation to (README X30).</summary>
/// <param name="Hash">The hash of the file's entries; a file edited after the preview has another one.</param>
/// <param name="Entries">How many entries the file has.</param>
/// <param name="Decided">How many of them carry a decision.</param>
public sealed record ReviewFilePreview(string Hash, int Entries, int Decided);
