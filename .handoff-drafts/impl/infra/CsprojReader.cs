using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the PackageReference items of a .csproj. A version held centrally reads as an empty version here; Directory.Packages.props has it.</summary>
internal sealed class CsprojReader : IManifestReader
{
    private static readonly Dictionary<string, string> Items = new(StringComparer.Ordinal) { ["PackageReference"] = "runtime" };

    public bool Handles(string path) => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<DepRecord> Read(string path, string text) => MsBuildItems.Read(path, text, Items);
}
