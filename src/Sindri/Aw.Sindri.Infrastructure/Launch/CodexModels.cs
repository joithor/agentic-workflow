namespace Aw.Sindri.Infrastructure.Launch;

#pragma warning disable MA0182 // until the launch probe fills TargetFacts.Models with it (M-3, after M-1's types)
/// <summary>Reads the output of 'codex debug models' (spec §3): keeps the entries whose visibility is 'list', in the order the CLI printed them.</summary>
internal static class CodexModels
{
    /// <summary>Parses the JSON.</summary>
    /// <param name="json">The command's stdout.</param>
    /// <returns>The listed models; empty when the text isn't the catalog JSON (the probe then shows 'no-model-list').</returns>
    public static IReadOnlyList<DiscoveredModel> Read(string json) => throw new NotSupportedException();
}
#pragma warning restore MA0182
