namespace Aw.Evidence.Core.Judging;

/// <summary>Screenshots for a visual critique; absolute paths.</summary>
/// <param name="AfterScreenshot">The screenshot under review (or its changed region).</param>
/// <param name="BaselineScreenshot">The approved baseline (or its matching region), when there is one.</param>
/// <param name="EvidenceDirectory">The run directory.</param>
public sealed record VisualCritiqueRequest(string AfterScreenshot, string? BaselineScreenshot, string EvidenceDirectory);
