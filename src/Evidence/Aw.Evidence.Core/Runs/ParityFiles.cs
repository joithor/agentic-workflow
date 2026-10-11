namespace Aw.Evidence.Core.Runs;

/// <summary>A frame's images (absolute paths).</summary>
/// <param name="Design">The design crop.</param>
/// <param name="Implementation">The implementation crop.</param>
/// <param name="Diff">The pixel diff.</param>
/// <param name="SideBySide">Design | implementation | diff.</param>
public sealed record ParityFiles(string Design, string Implementation, string Diff, string SideBySide)
{
    /// <summary>The four files for frame <paramref name="frame"/> in <paramref name="runDirectory"/>.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="frame">The frame's name.</param>
    /// <returns>The paths.</returns>
    public static ParityFiles For(string runDirectory, string frame) => new(
        Files.RunPath.Combine(runDirectory, $"{frame}-design.png"),
        Files.RunPath.Combine(runDirectory, $"{frame}-implementation.png"),
        Files.RunPath.Combine(runDirectory, $"{frame}-diff.png"),
        Files.RunPath.Combine(runDirectory, $"{frame}-side-by-side.png"));
}
