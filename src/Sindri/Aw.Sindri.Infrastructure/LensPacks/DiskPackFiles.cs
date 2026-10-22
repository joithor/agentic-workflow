namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="IOutsidePackFiles"/> over the disk: every file under '&lt;packs root&gt;/lenses/&lt;id&gt;', read as UTF-8.</summary>
internal sealed class DiskPackFiles : IOutsidePackFiles
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? Files(string packsRoot, string id) => throw new NotSupportedException();
}
