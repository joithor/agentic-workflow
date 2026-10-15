namespace Aw.Context.Core.Review;

/// <summary>A review file written under the exports folder for 'map review --export'.</summary>
/// <param name="Path">The file (mode 0600).</param>
/// <param name="Entries">How many entries it holds.</param>
public sealed record ReviewExported(string Path, int Entries);
