using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the PackageVersion and GlobalPackageReference items of Directory.Packages.props (central package management).</summary>
internal sealed class CentralPackagesReader : IManifestReader
{
    private static readonly Dictionary<string, string> Items = new(StringComparer.Ordinal)
    {
        ["PackageVersion"] = "central",
        ["GlobalPackageReference"] = "global",
    };

    public bool Handles(string path) => string.Equals(ManifestPath.FileName(path), "Directory.Packages.props", StringComparison.Ordinal);

    public IReadOnlyList<DepRecord> Read(string path, string text) => MsBuildItems.Read(path, text, Items);
}
