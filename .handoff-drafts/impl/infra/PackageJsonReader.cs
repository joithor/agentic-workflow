using System.Text.Json;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the dependency sections of a package.json. Only string versions count; anything else in a section is skipped.</summary>
internal sealed class PackageJsonReader : IManifestReader
{
    private const int MaxJsonDepth = 32;

    private static readonly (string Section, string Kind)[] Sections =
    [
        ("dependencies", "runtime"),
        ("devDependencies", "dev"),
        ("peerDependencies", "peer"),
        ("optionalDependencies", "optional"),
    ];

    public bool Handles(string path) => string.Equals(ManifestPath.FileName(path), "package.json", StringComparison.Ordinal);

    public IReadOnlyList<DepRecord> Read(string path, string text)
    {
        var set = new DepRowSet();
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxJsonDepth, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddSections(path, document.RootElement, set);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return set.Rows;
    }

    private static void AddSections(string path, JsonElement root, DepRowSet set)
    {
        foreach (var (section, kind) in Sections)
        {
            if (!root.TryGetProperty(section, out var entries) || entries.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in entries.EnumerateObject().Where(entry => entry.Value.ValueKind == JsonValueKind.String))
            {
                var version = entry.Value.GetString() ?? string.Empty;
                set.Add(new DepRecord(path, entry.Name, version, kind, Tags(version)));
            }
        }
    }

    private static string Tags(string version) => version switch
    {
        _ when version.StartsWith("workspace:", StringComparison.Ordinal) => "workspace",
        _ when version.StartsWith("file:", StringComparison.Ordinal) || version.StartsWith("link:", StringComparison.Ordinal) => "local",
        _ when version.Contains("://", StringComparison.Ordinal) || version.StartsWith("git+", StringComparison.Ordinal) || version.StartsWith("github:", StringComparison.Ordinal) => "url",
        _ => string.Empty,
    };
}
