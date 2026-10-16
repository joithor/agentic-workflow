namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>A measured map and where its report was written.</summary>
/// <param name="Report">The report.</param>
/// <param name="Path">The file under $AW_SINDRI_HOME/context/eval/map/.</param>
public sealed record MapQualityRun(MapQualityReport Report, string Path);
