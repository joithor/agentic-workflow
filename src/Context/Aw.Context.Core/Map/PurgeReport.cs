namespace Aw.Context.Core.Map;

/// <summary>What one purge removed.</summary>
/// <param name="Exports">Expired export files deleted.</param>
/// <param name="Versions">Retained map versions deleted.</param>
public sealed record PurgeReport(int Exports, int Versions);
