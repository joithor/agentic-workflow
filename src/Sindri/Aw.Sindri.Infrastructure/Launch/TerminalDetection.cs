namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The detection <c lang="text">sindri launch init</c> runs, and the launcher runs in memory when there is no launcher.json (launcher spec §4).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a).")]
internal static class TerminalDetection
{
    internal static ValueTask<DetectedTerminal> Detect(DetectionInputs inputs, CancellationToken cancellationToken) =>
        throw new NotSupportedException("TerminalDetection: make the detection tests pass (U7)");
}
