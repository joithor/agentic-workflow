using System.Reflection;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="ILensPackSource"/> over the files this assembly embeds as 'lens-packs/&lt;id&gt;/&lt;path&gt;' (the csproj embeds config/packs/lenses).</summary>
internal sealed class EmbeddedLensPackSource : ILensPackSource
{
    private const string Prefix = "lens-packs/";

    private readonly Assembly _assembly = typeof(EmbeddedLensPackSource).Assembly;

    public IReadOnlyList<string> Ids() =>
        [.. _assembly.GetManifestResourceNames().Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal)).Select(static n => n[Prefix.Length..].Split('/')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public IReadOnlyDictionary<string, string>? Files(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var root = $"{Prefix}{id}/";
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in _assembly.GetManifestResourceNames().Where(n => n.StartsWith(root, StringComparison.Ordinal)))
        {
            using var stream = _assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            files[name[root.Length..]] = reader.ReadToEnd();
        }

        return files.Count == 0 ? null : files;
    }
}
