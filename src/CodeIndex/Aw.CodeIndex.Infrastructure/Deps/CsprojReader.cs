using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the dependencies of *.csproj files.</summary>
internal sealed class CsprojReader : IManifestReader
{
    public bool Handles(string path) => throw new NotSupportedException();

    public IReadOnlyList<DepRecord> Read(string path, string text) => throw new NotSupportedException();
}
