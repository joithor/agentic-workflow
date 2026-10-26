using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Launch;

#pragma warning disable MA0182 // until the launch probe fills TargetFacts.Models with it (M-3, after M-1's types)
/// <summary>Reads the output of 'codex debug models' (spec §3): keeps the entries whose visibility is 'list', in the order the CLI printed them.</summary>
internal static class CodexModels
{
    private const string Source = "codex debug models";

    /// <summary>Parses the JSON.</summary>
    /// <param name="json">The command's stdout.</param>
    /// <returns>The listed models; empty when the text isn't the catalog JSON (the probe then shows 'no-model-list').</returns>
    public static IReadOnlyList<DiscoveredModel> Read(string json)
    {
        var parsed = AwJson.Deserialize<CodexModelsDocument>(System.Text.Encoding.UTF8.GetBytes(json), LauncherConfigFile.Json);
        return parsed.IsError || parsed.Value.Models is not { } rows
            ? []
            : [.. rows.Where(Listed).Select(Model).OfType<DiscoveredModel>()];
    }

    private static bool Listed(CodexModelRow row) => string.Equals(row.Visibility, "list", StringComparison.Ordinal);

    private static DiscoveredModel? Model(CodexModelRow row) =>
        row.Slug is { Length: > 0 } slug ? new DiscoveredModel(slug, row.DisplayName ?? slug, row.Priority, Source) : null;
}
#pragma warning restore MA0182
