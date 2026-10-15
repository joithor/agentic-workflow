namespace Aw.Context.Core.Lenses;

/// <summary>The symbol column of the code band: file-level only until the code index exists (Phase 9).</summary>
/// <param name="Status">'unavailable'.</param>
/// <param name="Reason">'index not built'.</param>
public sealed record LensSymbols(string Status, string Reason);
