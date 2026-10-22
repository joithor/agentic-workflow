using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>An <see cref="IOutsidePackFiles"/> in memory, held to <see cref="PackFilesContract"/>: packs a test puts under a packs root.</summary>
internal sealed class InMemoryPackFiles : IOutsidePackFiles
{
    private readonly Dictionary<(string Root, string Id), Dictionary<string, string>> _packs = [];

    internal void Put(string packsRoot, string id, string path, string text)
    {
        if (!_packs.TryGetValue((packsRoot, id), out var files))
        {
            _packs[(packsRoot, id)] = files = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        files[path] = text;
    }

    public IReadOnlyDictionary<string, string>? Files(string packsRoot, string id) => _packs.TryGetValue((packsRoot, id), out var files) ? files : null;
}
